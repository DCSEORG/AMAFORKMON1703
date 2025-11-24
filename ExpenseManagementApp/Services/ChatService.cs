using Azure.AI.OpenAI;
using Azure.Identity;
using Azure.Core;
using OpenAI.Chat;
using ExpenseManagementApp.Models;
using System.Text.Json;
using System.ClientModel;

namespace ExpenseManagementApp.Services;

public class ChatService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<ChatService> _logger;
    private readonly ExpenseService _expenseService;
    private AzureOpenAIClient? _openAIClient;
    private ChatClient? _chatClient;
    private bool _isConfigured = false;
    private string? _lastError;

    public ChatService(IConfiguration configuration, ILogger<ChatService> logger, ExpenseService expenseService)
    {
        _configuration = configuration;
        _logger = logger;
        _expenseService = expenseService;
        InitializeClient();
    }

    private void InitializeClient()
    {
        try
        {
            var openAIEndpoint = _configuration["OpenAI__Endpoint"];
            var openAIModelName = _configuration["OpenAI__DeploymentName"];
            var managedIdentityClientId = _configuration["AZURE_CLIENT_ID"] ?? _configuration["ManagedIdentityClientId"];

            if (string.IsNullOrEmpty(openAIEndpoint) || string.IsNullOrEmpty(openAIModelName))
            {
                _logger.LogWarning("OpenAI configuration not found. Chat service will return dummy responses.");
                _isConfigured = false;
                return;
            }

            // Use ManagedIdentityCredential with explicit client ID
            TokenCredential credential;
            
            if (!string.IsNullOrEmpty(managedIdentityClientId))
            {
                _logger.LogInformation("Using ManagedIdentityCredential with client ID: {ClientId}", managedIdentityClientId);
                credential = new ManagedIdentityCredential(managedIdentityClientId);
            }
            else
            {
                _logger.LogInformation("Using DefaultAzureCredential");
                credential = new DefaultAzureCredential();
            }

            _openAIClient = new AzureOpenAIClient(new Uri(openAIEndpoint), credential);
            _chatClient = _openAIClient.GetChatClient(openAIModelName);
            _isConfigured = true;
            _lastError = null;

            _logger.LogInformation("Chat service initialized successfully with endpoint: {Endpoint}", openAIEndpoint);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize OpenAI client");
            _isConfigured = false;
            _lastError = $"Failed to initialize AI: {ex.Message}";
        }
    }

    public async Task<string> GetChatResponseAsync(string userMessage, List<ChatMessage> conversationHistory)
    {
        if (!_isConfigured || _chatClient == null)
        {
            return GetDummyResponse(userMessage);
        }

        try
        {
            // Build conversation with system prompt
            var messages = new List<ChatMessage>
            {
                new SystemChatMessage(GetSystemPrompt())
            };

            // Add conversation history
            messages.AddRange(conversationHistory);

            // Add current user message
            messages.Add(new UserChatMessage(userMessage));

            // Define function tools
            var tools = new List<ChatTool>
            {
                ChatTool.CreateFunctionTool(
                    functionName: "get_all_expenses",
                    functionDescription: "Retrieves all expenses from the database",
                    functionParameters: BinaryData.FromString("{\"type\":\"object\",\"properties\":{},\"required\":[]}")
                ),
                ChatTool.CreateFunctionTool(
                    functionName: "get_expenses_by_status",
                    functionDescription: "Retrieves expenses filtered by status (1=Draft, 2=Submitted, 3=Approved, 4=Rejected)",
                    functionParameters: BinaryData.FromString("{\"type\":\"object\",\"properties\":{\"statusId\":{\"type\":\"integer\",\"description\":\"Status ID: 1=Draft, 2=Submitted, 3=Approved, 4=Rejected\"}},\"required\":[\"statusId\"]}")
                ),
                ChatTool.CreateFunctionTool(
                    functionName: "get_expense_by_id",
                    functionDescription: "Retrieves a specific expense by its ID",
                    functionParameters: BinaryData.FromString("{\"type\":\"object\",\"properties\":{\"expenseId\":{\"type\":\"integer\",\"description\":\"The expense ID\"}},\"required\":[\"expenseId\"]}")
                ),
                ChatTool.CreateFunctionTool(
                    functionName: "create_expense",
                    functionDescription: "Creates a new expense",
                    functionParameters: BinaryData.FromString("{\"type\":\"object\",\"properties\":{\"userId\":{\"type\":\"integer\",\"description\":\"User ID (use 1 for Alice)\"},\"categoryId\":{\"type\":\"integer\",\"description\":\"Category ID: 1=Travel, 2=Meals, 3=Supplies, 4=Accommodation, 5=Other\"},\"amount\":{\"type\":\"number\",\"description\":\"Expense amount in GBP\"},\"expenseDate\":{\"type\":\"string\",\"description\":\"Expense date in ISO format\"},\"description\":{\"type\":\"string\",\"description\":\"Expense description\"}},\"required\":[\"userId\",\"categoryId\",\"amount\",\"expenseDate\"]}")
                ),
                ChatTool.CreateFunctionTool(
                    functionName: "submit_expense",
                    functionDescription: "Submits a draft expense for approval",
                    functionParameters: BinaryData.FromString("{\"type\":\"object\",\"properties\":{\"expenseId\":{\"type\":\"integer\",\"description\":\"The expense ID to submit\"}},\"required\":[\"expenseId\"]}")
                ),
                ChatTool.CreateFunctionTool(
                    functionName: "approve_expense",
                    functionDescription: "Approves a submitted expense",
                    functionParameters: BinaryData.FromString("{\"type\":\"object\",\"properties\":{\"expenseId\":{\"type\":\"integer\",\"description\":\"The expense ID to approve\"},\"reviewerId\":{\"type\":\"integer\",\"description\":\"Reviewer user ID (use 2 for Bob)\"}},\"required\":[\"expenseId\",\"reviewerId\"]}")
                ),
                ChatTool.CreateFunctionTool(
                    functionName: "reject_expense",
                    functionDescription: "Rejects a submitted expense",
                    functionParameters: BinaryData.FromString("{\"type\":\"object\",\"properties\":{\"expenseId\":{\"type\":\"integer\",\"description\":\"The expense ID to reject\"},\"reviewerId\":{\"type\":\"integer\",\"description\":\"Reviewer user ID (use 2 for Bob)\"}},\"required\":[\"expenseId\",\"reviewerId\"]}")
                ),
                ChatTool.CreateFunctionTool(
                    functionName: "get_categories",
                    functionDescription: "Retrieves all available expense categories",
                    functionParameters: BinaryData.FromString("{\"type\":\"object\",\"properties\":{},\"required\":[]}")
                ),
                ChatTool.CreateFunctionTool(
                    functionName: "get_statuses",
                    functionDescription: "Retrieves all available expense statuses",
                    functionParameters: BinaryData.FromString("{\"type\":\"object\",\"properties\":{},\"required\":[]}")
                )
            };

            var chatOptions = new ChatCompletionOptions();
            foreach (var tool in tools)
            {
                chatOptions.Tools.Add(tool);
            }

            // First API call
            ChatCompletion completion = await _chatClient.CompleteChatAsync(messages, chatOptions);

            // Handle tool calls in a loop
            while (completion.FinishReason == ChatFinishReason.ToolCalls)
            {
                // Add assistant message with tool calls
                messages.Add(new AssistantChatMessage(completion));

                // Execute each tool call
                foreach (var toolCall in completion.ToolCalls)
                {
                    var functionName = toolCall.FunctionName;
                    var functionArgs = toolCall.FunctionArguments;

                    _logger.LogInformation("Executing function: {FunctionName} with args: {Args}", functionName, functionArgs);

                    string functionResult = await ExecuteFunctionAsync(functionName, functionArgs);

                    // Add function result to messages
                    messages.Add(new ToolChatMessage(toolCall.Id, functionResult));
                }

                // Make another API call with the function results
                completion = await _chatClient.CompleteChatAsync(messages, chatOptions);
            }

            _lastError = null;
            return completion.Content[0].Text;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling OpenAI API");
            _lastError = $"AI Error: {ex.Message}";
            return $"I apologize, but I encountered an error processing your request: {ex.Message}";
        }
    }

    private async Task<string> ExecuteFunctionAsync(string functionName, BinaryData functionArgs)
    {
        try
        {
            var args = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(functionArgs.ToString());

            switch (functionName)
            {
                case "get_all_expenses":
                    var allExpenses = await _expenseService.GetAllExpensesAsync();
                    return JsonSerializer.Serialize(allExpenses);

                case "get_expenses_by_status":
                    var statusId = args!["statusId"].GetInt32();
                    var expensesByStatus = await _expenseService.GetExpensesByStatusAsync(statusId);
                    return JsonSerializer.Serialize(expensesByStatus);

                case "get_expense_by_id":
                    var expenseId = args!["expenseId"].GetInt32();
                    var expense = await _expenseService.GetExpenseByIdAsync(expenseId);
                    return JsonSerializer.Serialize(expense);

                case "create_expense":
                    var createDto = new CreateExpenseDto
                    {
                        UserId = args!["userId"].GetInt32(),
                        CategoryId = args["categoryId"].GetInt32(),
                        Amount = args["amount"].GetDecimal(),
                        ExpenseDate = DateTime.Parse(args["expenseDate"].GetString()!),
                        Description = args.ContainsKey("description") ? args["description"].GetString() : null
                    };
                    var newExpenseId = await _expenseService.CreateExpenseAsync(createDto);
                    return JsonSerializer.Serialize(new { success = newExpenseId > 0, expenseId = newExpenseId });

                case "submit_expense":
                    var submitExpenseId = args!["expenseId"].GetInt32();
                    var submitSuccess = await _expenseService.SubmitExpenseAsync(submitExpenseId);
                    return JsonSerializer.Serialize(new { success = submitSuccess });

                case "approve_expense":
                    var approveExpenseId = args!["expenseId"].GetInt32();
                    var reviewerId = args["reviewerId"].GetInt32();
                    var approveSuccess = await _expenseService.ApproveExpenseAsync(approveExpenseId, reviewerId);
                    return JsonSerializer.Serialize(new { success = approveSuccess });

                case "reject_expense":
                    var rejectExpenseId = args!["expenseId"].GetInt32();
                    var rejectReviewerId = args["reviewerId"].GetInt32();
                    var rejectSuccess = await _expenseService.RejectExpenseAsync(rejectExpenseId, rejectReviewerId);
                    return JsonSerializer.Serialize(new { success = rejectSuccess });

                case "get_categories":
                    var categories = await _expenseService.GetAllCategoriesAsync();
                    return JsonSerializer.Serialize(categories);

                case "get_statuses":
                    var statuses = await _expenseService.GetAllStatusesAsync();
                    return JsonSerializer.Serialize(statuses);

                default:
                    return JsonSerializer.Serialize(new { error = $"Unknown function: {functionName}" });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing function {FunctionName}", functionName);
            return JsonSerializer.Serialize(new { error = ex.Message });
        }
    }

    private string GetSystemPrompt()
    {
        return @"You are an AI assistant for an Expense Management System. You help users manage their business expenses.

You have access to the following functions to interact with the database:
- get_all_expenses: View all expenses
- get_expenses_by_status: Filter expenses by status (1=Draft, 2=Submitted, 3=Approved, 4=Rejected)
- get_expense_by_id: Get details of a specific expense
- create_expense: Create a new expense (userId=1 for Alice, categories: 1=Travel, 2=Meals, 3=Supplies, 4=Accommodation, 5=Other)
- submit_expense: Submit a draft expense for approval
- approve_expense: Approve a submitted expense (reviewerId=2 for Bob)
- reject_expense: Reject a submitted expense (reviewerId=2 for Bob)
- get_categories: List all expense categories
- get_statuses: List all expense statuses

When presenting lists of expenses or data:
- Format amounts as currency (£XX.XX)
- Use clear, structured formatting with bullet points or numbered lists
- Include relevant details like dates, categories, and statuses
- Be concise but informative

Always be helpful, professional, and accurate when responding to user queries about expenses.";
    }

    private string GetDummyResponse(string userMessage)
    {
        var lowerMessage = userMessage.ToLower();

        if (lowerMessage.Contains("expense") || lowerMessage.Contains("list") || lowerMessage.Contains("show"))
        {
            return @"⚠️ **Gen AI Services Not Deployed**

The AI chat feature requires Azure OpenAI services to be deployed. Currently, I'm running in dummy mode.

To enable full AI-powered chat capabilities, run: `./deploy-with-chat.sh`

This will deploy:
- Azure OpenAI with GPT-4o model
- Azure Cognitive Search for enhanced responses
- Full function calling to interact with your expense database

For now, you can still use the main application interface to manage your expenses.";
        }

        return "⚠️ Gen AI services are not deployed. Run `./deploy-with-chat.sh` to enable AI chat features.";
    }

    public bool IsConfigured() => _isConfigured;
    public string? GetLastError() => _lastError;
}
