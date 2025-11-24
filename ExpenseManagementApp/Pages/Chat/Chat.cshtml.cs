using Microsoft.AspNetCore.Mvc.RazorPages;
using ExpenseManagementApp.Services;

namespace ExpenseManagementApp.Pages.Chat;

public class ChatModel : PageModel
{
    private readonly ChatService _chatService;

    public ChatModel(ChatService chatService)
    {
        _chatService = chatService;
    }

    public bool IsGenAIConfigured { get; set; }

    public void OnGet()
    {
        IsGenAIConfigured = _chatService.IsConfigured();
    }
}
