using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ExpenseManagementApp.Services;
using ExpenseManagementApp.Models;

namespace ExpenseManagementApp.Pages;

public class IndexModel : PageModel
{
    private readonly ILogger<IndexModel> _logger;
    private readonly ExpenseService _expenseService;

    public IndexModel(ILogger<IndexModel> logger, ExpenseService expenseService)
    {
        _logger = logger;
        _expenseService = expenseService;
    }

    public List<Expense> Expenses { get; set; } = new();
    public List<ExpenseCategory> Categories { get; set; } = new();
    public List<ExpenseStatus> Statuses { get; set; } = new();
    public string? ErrorMessage { get; set; }
    public int? StatusFilter { get; set; }

    public async Task OnGetAsync(int? statusFilter)
    {
        StatusFilter = statusFilter;

        if (statusFilter.HasValue)
        {
            Expenses = await _expenseService.GetExpensesByStatusAsync(statusFilter.Value);
        }
        else
        {
            Expenses = await _expenseService.GetAllExpensesAsync();
        }

        Categories = await _expenseService.GetAllCategoriesAsync();
        Statuses = await _expenseService.GetAllStatusesAsync();
        ErrorMessage = _expenseService.GetLastError();
    }

    public async Task<IActionResult> OnPostCreateAsync(int categoryId, decimal amount, DateTime expenseDate, string? description, int userId = 1)
    {
        var dto = new CreateExpenseDto
        {
            UserId = userId,
            CategoryId = categoryId,
            Amount = amount,
            ExpenseDate = expenseDate,
            Description = description
        };

        await _expenseService.CreateExpenseAsync(dto);
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostSubmitAsync(int expenseId)
    {
        await _expenseService.SubmitExpenseAsync(expenseId);
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostApproveAsync(int expenseId, int reviewerId = 2)
    {
        await _expenseService.ApproveExpenseAsync(expenseId, reviewerId);
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostRejectAsync(int expenseId, int reviewerId = 2)
    {
        await _expenseService.RejectExpenseAsync(expenseId, reviewerId);
        return RedirectToPage();
    }
}
