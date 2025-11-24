using Microsoft.Data.SqlClient;
using Azure.Identity;
using Azure.Core;
using ExpenseManagementApp.Models;
using System.Data;

namespace ExpenseManagementApp.Services;

public class ExpenseService
{
    private readonly string _connectionString;
    private readonly string? _managedIdentityClientId;
    private readonly ILogger<ExpenseService> _logger;
    private string? _lastError;

    public ExpenseService(IConfiguration configuration, ILogger<ExpenseService> logger)
    {
        var sqlServer = configuration["SqlServer"] ?? "sql-expensemgmt-REPLACE.database.windows.net";
        var database = configuration["Database"] ?? "Northwind";
        _managedIdentityClientId = configuration["AZURE_CLIENT_ID"] ?? configuration["ManagedIdentityClientId"];
        _logger = logger;

        // Connection string with Managed Identity or Default Azure Credential
        var authMode = configuration["AuthenticationMode"] ?? "Active Directory Managed Identity";
        _connectionString = $"Server=tcp:{sqlServer};Database={database};Authentication={authMode};";
        
        if (!string.IsNullOrEmpty(_managedIdentityClientId) && authMode == "Active Directory Managed Identity")
        {
            _connectionString += $"User Id={_managedIdentityClientId};";
        }
    }

    public string? GetLastError() => _lastError;

    private async Task<SqlConnection> GetConnectionAsync()
    {
        var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        return connection;
    }

    public async Task<List<Expense>> GetAllExpensesAsync()
    {
        try
        {
            using var connection = await GetConnectionAsync();
            using var command = new SqlCommand("GetAllExpenses", connection)
            {
                CommandType = CommandType.StoredProcedure
            };

            var expenses = new List<Expense>();
            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                expenses.Add(MapExpenseFromReader(reader));
            }

            _lastError = null;
            return expenses;
        }
        catch (Exception ex)
        {
            _lastError = $"Database connection error in GetAllExpensesAsync at ExpenseService.cs:57. {GetManagedIdentityErrorHelp(ex)}";
            _logger.LogError(ex, "Failed to get expenses from database");
            return GetDummyExpenses();
        }
    }

    public async Task<Expense?> GetExpenseByIdAsync(int expenseId)
    {
        try
        {
            using var connection = await GetConnectionAsync();
            using var command = new SqlCommand("GetExpenseById", connection)
            {
                CommandType = CommandType.StoredProcedure
            };
            command.Parameters.AddWithValue("@ExpenseId", expenseId);

            using var reader = await command.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                _lastError = null;
                return MapExpenseFromReader(reader);
            }
            return null;
        }
        catch (Exception ex)
        {
            _lastError = $"Database connection error in GetExpenseByIdAsync at ExpenseService.cs:86. {GetManagedIdentityErrorHelp(ex)}";
            _logger.LogError(ex, "Failed to get expense from database");
            return GetDummyExpenses().FirstOrDefault(e => e.ExpenseId == expenseId);
        }
    }

    public async Task<List<Expense>> GetExpensesByStatusAsync(int statusId)
    {
        try
        {
            using var connection = await GetConnectionAsync();
            using var command = new SqlCommand("GetExpensesByStatus", connection)
            {
                CommandType = CommandType.StoredProcedure
            };
            command.Parameters.AddWithValue("@StatusId", statusId);

            var expenses = new List<Expense>();
            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                expenses.Add(MapExpenseFromReader(reader));
            }

            _lastError = null;
            return expenses;
        }
        catch (Exception ex)
        {
            _lastError = $"Database connection error in GetExpensesByStatusAsync at ExpenseService.cs:117. {GetManagedIdentityErrorHelp(ex)}";
            _logger.LogError(ex, "Failed to get expenses by status from database");
            return GetDummyExpenses().Where(e => e.StatusId == statusId).ToList();
        }
    }

    public async Task<int> CreateExpenseAsync(CreateExpenseDto dto)
    {
        try
        {
            using var connection = await GetConnectionAsync();
            using var command = new SqlCommand("CreateExpense", connection)
            {
                CommandType = CommandType.StoredProcedure
            };
            
            command.Parameters.AddWithValue("@UserId", dto.UserId);
            command.Parameters.AddWithValue("@CategoryId", dto.CategoryId);
            command.Parameters.AddWithValue("@AmountMinor", (int)(dto.Amount * 100));
            command.Parameters.AddWithValue("@Currency", dto.Currency);
            command.Parameters.AddWithValue("@ExpenseDate", dto.ExpenseDate);
            command.Parameters.AddWithValue("@Description", (object?)dto.Description ?? DBNull.Value);
            command.Parameters.AddWithValue("@ReceiptFile", (object?)dto.ReceiptFile ?? DBNull.Value);

            var result = await command.ExecuteScalarAsync();
            _lastError = null;
            return Convert.ToInt32(result);
        }
        catch (Exception ex)
        {
            _lastError = $"Database connection error in CreateExpenseAsync at ExpenseService.cs:151. {GetManagedIdentityErrorHelp(ex)}";
            _logger.LogError(ex, "Failed to create expense in database");
            return -1; // Return -1 to indicate error
        }
    }

    public async Task<bool> UpdateExpenseAsync(UpdateExpenseDto dto)
    {
        try
        {
            using var connection = await GetConnectionAsync();
            using var command = new SqlCommand("UpdateExpense", connection)
            {
                CommandType = CommandType.StoredProcedure
            };
            
            command.Parameters.AddWithValue("@ExpenseId", dto.ExpenseId);
            command.Parameters.AddWithValue("@CategoryId", dto.CategoryId);
            command.Parameters.AddWithValue("@AmountMinor", (int)(dto.Amount * 100));
            command.Parameters.AddWithValue("@Currency", dto.Currency);
            command.Parameters.AddWithValue("@ExpenseDate", dto.ExpenseDate);
            command.Parameters.AddWithValue("@Description", (object?)dto.Description ?? DBNull.Value);
            command.Parameters.AddWithValue("@ReceiptFile", (object?)dto.ReceiptFile ?? DBNull.Value);

            await command.ExecuteNonQueryAsync();
            _lastError = null;
            return true;
        }
        catch (Exception ex)
        {
            _lastError = $"Database connection error in UpdateExpenseAsync at ExpenseService.cs:185. {GetManagedIdentityErrorHelp(ex)}";
            _logger.LogError(ex, "Failed to update expense in database");
            return false;
        }
    }

    public async Task<bool> SubmitExpenseAsync(int expenseId)
    {
        try
        {
            using var connection = await GetConnectionAsync();
            using var command = new SqlCommand("SubmitExpense", connection)
            {
                CommandType = CommandType.StoredProcedure
            };
            command.Parameters.AddWithValue("@ExpenseId", expenseId);

            await command.ExecuteNonQueryAsync();
            _lastError = null;
            return true;
        }
        catch (Exception ex)
        {
            _lastError = $"Database connection error in SubmitExpenseAsync at ExpenseService.cs:208. {GetManagedIdentityErrorHelp(ex)}";
            _logger.LogError(ex, "Failed to submit expense in database");
            return false;
        }
    }

    public async Task<bool> ApproveExpenseAsync(int expenseId, int reviewerId)
    {
        try
        {
            using var connection = await GetConnectionAsync();
            using var command = new SqlCommand("ApproveExpense", connection)
            {
                CommandType = CommandType.StoredProcedure
            };
            command.Parameters.AddWithValue("@ExpenseId", expenseId);
            command.Parameters.AddWithValue("@ReviewerId", reviewerId);

            await command.ExecuteNonQueryAsync();
            _lastError = null;
            return true;
        }
        catch (Exception ex)
        {
            _lastError = $"Database connection error in ApproveExpenseAsync at ExpenseService.cs:232. {GetManagedIdentityErrorHelp(ex)}";
            _logger.LogError(ex, "Failed to approve expense in database");
            return false;
        }
    }

    public async Task<bool> RejectExpenseAsync(int expenseId, int reviewerId)
    {
        try
        {
            using var connection = await GetConnectionAsync();
            using var command = new SqlCommand("RejectExpense", connection)
            {
                CommandType = CommandType.StoredProcedure
            };
            command.Parameters.AddWithValue("@ExpenseId", expenseId);
            command.Parameters.AddWithValue("@ReviewerId", reviewerId);

            await command.ExecuteNonQueryAsync();
            _lastError = null;
            return true;
        }
        catch (Exception ex)
        {
            _lastError = $"Database connection error in RejectExpenseAsync at ExpenseService.cs:256. {GetManagedIdentityErrorHelp(ex)}";
            _logger.LogError(ex, "Failed to reject expense in database");
            return false;
        }
    }

    public async Task<List<ExpenseCategory>> GetAllCategoriesAsync()
    {
        try
        {
            using var connection = await GetConnectionAsync();
            using var command = new SqlCommand("GetAllCategories", connection)
            {
                CommandType = CommandType.StoredProcedure
            };

            var categories = new List<ExpenseCategory>();
            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                categories.Add(new ExpenseCategory
                {
                    CategoryId = reader.GetInt32(0),
                    CategoryName = reader.GetString(1),
                    IsActive = reader.GetBoolean(2)
                });
            }

            _lastError = null;
            return categories;
        }
        catch (Exception ex)
        {
            _lastError = $"Database connection error in GetAllCategoriesAsync at ExpenseService.cs:290. {GetManagedIdentityErrorHelp(ex)}";
            _logger.LogError(ex, "Failed to get categories from database");
            return GetDummyCategories();
        }
    }

    public async Task<List<ExpenseStatus>> GetAllStatusesAsync()
    {
        try
        {
            using var connection = await GetConnectionAsync();
            using var command = new SqlCommand("GetAllStatuses", connection)
            {
                CommandType = CommandType.StoredProcedure
            };

            var statuses = new List<ExpenseStatus>();
            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                statuses.Add(new ExpenseStatus
                {
                    StatusId = reader.GetInt32(0),
                    StatusName = reader.GetString(1)
                });
            }

            _lastError = null;
            return statuses;
        }
        catch (Exception ex)
        {
            _lastError = $"Database connection error in GetAllStatusesAsync at ExpenseService.cs:324. {GetManagedIdentityErrorHelp(ex)}";
            _logger.LogError(ex, "Failed to get statuses from database");
            return GetDummyStatuses();
        }
    }

    private Expense MapExpenseFromReader(SqlDataReader reader)
    {
        return new Expense
        {
            ExpenseId = reader.GetInt32(reader.GetOrdinal("ExpenseId")),
            UserId = reader.GetInt32(reader.GetOrdinal("UserId")),
            UserName = reader.GetString(reader.GetOrdinal("UserName")),
            Email = reader.GetString(reader.GetOrdinal("Email")),
            CategoryId = reader.GetInt32(reader.GetOrdinal("CategoryId")),
            CategoryName = reader.GetString(reader.GetOrdinal("CategoryName")),
            StatusId = reader.GetInt32(reader.GetOrdinal("StatusId")),
            StatusName = reader.GetString(reader.GetOrdinal("StatusName")),
            AmountMinor = reader.GetInt32(reader.GetOrdinal("AmountMinor")),
            AmountDecimal = reader.GetDecimal(reader.GetOrdinal("AmountDecimal")),
            Currency = reader.GetString(reader.GetOrdinal("Currency")),
            ExpenseDate = reader.GetDateTime(reader.GetOrdinal("ExpenseDate")),
            Description = reader.IsDBNull(reader.GetOrdinal("Description")) ? null : reader.GetString(reader.GetOrdinal("Description")),
            ReceiptFile = reader.IsDBNull(reader.GetOrdinal("ReceiptFile")) ? null : reader.GetString(reader.GetOrdinal("ReceiptFile")),
            SubmittedAt = reader.IsDBNull(reader.GetOrdinal("SubmittedAt")) ? null : reader.GetDateTime(reader.GetOrdinal("SubmittedAt")),
            ReviewedBy = reader.IsDBNull(reader.GetOrdinal("ReviewedBy")) ? null : reader.GetInt32(reader.GetOrdinal("ReviewedBy")),
            ReviewerName = reader.IsDBNull(reader.GetOrdinal("ReviewerName")) ? null : reader.GetString(reader.GetOrdinal("ReviewerName")),
            ReviewedAt = reader.IsDBNull(reader.GetOrdinal("ReviewedAt")) ? null : reader.GetDateTime(reader.GetOrdinal("ReviewedAt")),
            CreatedAt = reader.GetDateTime(reader.GetOrdinal("CreatedAt"))
        };
    }

    private string GetManagedIdentityErrorHelp(Exception ex)
    {
        var errorMessage = ex.Message.ToLower();
        
        if (errorMessage.Contains("managed identity") || errorMessage.Contains("authentication"))
        {
            return $"Managed Identity Error: Ensure the App Service managed identity (Client ID: {_managedIdentityClientId ?? "NOT SET"}) " +
                   "has been granted database permissions. Run the deployment script's database role assignment section. " +
                   "The managed identity needs db_datareader, db_datawriter, and EXECUTE permissions on the database.";
        }
        
        if (errorMessage.Contains("login failed") || errorMessage.Contains("cannot open database"))
        {
            return "Database Access Error: Verify SQL Server firewall rules allow Azure services, " +
                   "and the managed identity has been created as a user in the database with proper roles.";
        }

        return $"Error: {ex.Message}";
    }

    private List<Expense> GetDummyExpenses()
    {
        return new List<Expense>
        {
            new Expense
            {
                ExpenseId = 1,
                UserId = 1,
                UserName = "Alice Example",
                Email = "alice@example.co.uk",
                CategoryId = 1,
                CategoryName = "Travel",
                StatusId = 2,
                StatusName = "Submitted",
                AmountMinor = 2540,
                AmountDecimal = 25.40m,
                Currency = "GBP",
                ExpenseDate = DateTime.Now.AddDays(-5),
                Description = "Taxi from airport to client site",
                ReceiptFile = "/receipts/alice/taxi_oct20.jpg",
                SubmittedAt = DateTime.Now.AddDays(-4),
                CreatedAt = DateTime.Now.AddDays(-5)
            },
            new Expense
            {
                ExpenseId = 2,
                UserId = 1,
                UserName = "Alice Example",
                Email = "alice@example.co.uk",
                CategoryId = 2,
                CategoryName = "Meals",
                StatusId = 3,
                StatusName = "Approved",
                AmountMinor = 1425,
                AmountDecimal = 14.25m,
                Currency = "GBP",
                ExpenseDate = DateTime.Now.AddDays(-10),
                Description = "Client lunch meeting",
                ReceiptFile = "/receipts/alice/lunch_sep15.jpg",
                SubmittedAt = DateTime.Now.AddDays(-9),
                ReviewedBy = 2,
                ReviewerName = "Bob Manager",
                ReviewedAt = DateTime.Now.AddDays(-8),
                CreatedAt = DateTime.Now.AddDays(-10)
            }
        };
    }

    private List<ExpenseCategory> GetDummyCategories()
    {
        return new List<ExpenseCategory>
        {
            new ExpenseCategory { CategoryId = 1, CategoryName = "Travel", IsActive = true },
            new ExpenseCategory { CategoryId = 2, CategoryName = "Meals", IsActive = true },
            new ExpenseCategory { CategoryId = 3, CategoryName = "Supplies", IsActive = true },
            new ExpenseCategory { CategoryId = 4, CategoryName = "Accommodation", IsActive = true },
            new ExpenseCategory { CategoryId = 5, CategoryName = "Other", IsActive = true }
        };
    }

    private List<ExpenseStatus> GetDummyStatuses()
    {
        return new List<ExpenseStatus>
        {
            new ExpenseStatus { StatusId = 1, StatusName = "Draft" },
            new ExpenseStatus { StatusId = 2, StatusName = "Submitted" },
            new ExpenseStatus { StatusId = 3, StatusName = "Approved" },
            new ExpenseStatus { StatusId = 4, StatusName = "Rejected" }
        };
    }
}
