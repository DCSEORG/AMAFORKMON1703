using ExpenseManagementApp.Services;
using ExpenseManagementApp.Models;
using OpenAI.Chat;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorPages();
builder.Services.AddSingleton<ExpenseService>();
builder.Services.AddSingleton<ChatService>();

// Add API controllers and Swagger
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "Expense Management API", Version = "v1" });
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

// Enable Swagger in all environments for demo purposes
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Expense Management API v1");
    c.RoutePrefix = "swagger";
});

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();

app.MapRazorPages();
app.MapControllers();

// API Endpoints
var api = app.MapGroup("/api");

// Expenses API
api.MapGet("/expenses", async (ExpenseService service) =>
{
    var expenses = await service.GetAllExpensesAsync();
    return Results.Ok(expenses);
}).WithName("GetAllExpenses");

api.MapGet("/expenses/{id:int}", async (int id, ExpenseService service) =>
{
    var expense = await service.GetExpenseByIdAsync(id);
    return expense is not null ? Results.Ok(expense) : Results.NotFound();
}).WithName("GetExpenseById");

api.MapGet("/expenses/status/{statusId:int}", async (int statusId, ExpenseService service) =>
{
    var expenses = await service.GetExpensesByStatusAsync(statusId);
    return Results.Ok(expenses);
}).WithName("GetExpensesByStatus");

api.MapPost("/expenses", async (CreateExpenseDto dto, ExpenseService service) =>
{
    var expenseId = await service.CreateExpenseAsync(dto);
    return expenseId > 0 ? Results.Created($"/api/expenses/{expenseId}", new { expenseId }) : Results.BadRequest();
}).WithName("CreateExpense");

api.MapPut("/expenses", async (UpdateExpenseDto dto, ExpenseService service) =>
{
    var success = await service.UpdateExpenseAsync(dto);
    return success ? Results.Ok() : Results.BadRequest();
}).WithName("UpdateExpense");

api.MapPost("/expenses/{id:int}/submit", async (int id, ExpenseService service) =>
{
    var success = await service.SubmitExpenseAsync(id);
    return success ? Results.Ok() : Results.BadRequest();
}).WithName("SubmitExpense");

api.MapPost("/expenses/{id:int}/approve", async (int id, int reviewerId, ExpenseService service) =>
{
    var success = await service.ApproveExpenseAsync(id, reviewerId);
    return success ? Results.Ok() : Results.BadRequest();
}).WithName("ApproveExpense");

api.MapPost("/expenses/{id:int}/reject", async (int id, int reviewerId, ExpenseService service) =>
{
    var success = await service.RejectExpenseAsync(id, reviewerId);
    return success ? Results.Ok() : Results.BadRequest();
}).WithName("RejectExpense");

// Categories API
api.MapGet("/categories", async (ExpenseService service) =>
{
    var categories = await service.GetAllCategoriesAsync();
    return Results.Ok(categories);
}).WithName("GetAllCategories");

// Statuses API
api.MapGet("/statuses", async (ExpenseService service) =>
{
    var statuses = await service.GetAllStatusesAsync();
    return Results.Ok(statuses);
}).WithName("GetAllStatuses");

// Chat API
api.MapPost("/chat", async (ChatRequest request, ChatService chatService) =>
{
    var history = new List<ChatMessage>();
    
    // Convert request history to ChatMessage objects
    foreach (var msg in request.ConversationHistory ?? new())
    {
        if (msg.Role == "user")
            history.Add(new UserChatMessage(msg.Content));
        else if (msg.Role == "assistant")
            history.Add(new AssistantChatMessage(msg.Content));
    }
    
    var response = await chatService.GetChatResponseAsync(request.Message, history);
    return Results.Ok(new { response });
}).WithName("ChatWithAI");

app.Run();

// Request models
public record ChatRequest(string Message, List<ConversationMessage>? ConversationHistory);
public record ConversationMessage(string Role, string Content);
