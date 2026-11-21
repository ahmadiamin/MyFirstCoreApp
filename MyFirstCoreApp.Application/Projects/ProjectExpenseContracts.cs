using MyFirstCoreApp.Domain.Enums;

namespace MyFirstCoreApp.Application.Projects;

public sealed class ProjectExpenseRequest
{
    public DateOnly Date { get; set; }
    public int? ProjectId { get; set; }
    public int? ContactId { get; set; }
    public int ExpenseAccountId { get; set; }
    public decimal Amount { get; set; }
    public string Description { get; set; } = string.Empty;
    public string? Reference { get; set; }
}

public sealed class ProjectExpensePaymentRequest
{
    public int ProjectExpenseId { get; set; }
    public DateOnly Date { get; set; }
    public decimal Amount { get; set; }
    public int AccountId { get; set; }
    public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.BankTransfer;
    public string? Reference { get; set; }
    public string? Description { get; set; }
}

public sealed class ProjectExpenseDto
{
    public int Id { get; set; }
    public int Number { get; set; }
    public DateOnly Date { get; set; }
    public int? ProjectId { get; set; }
    public string? ProjectTitle { get; set; }
    public int? ContactId { get; set; }
    public string? ContactName { get; set; }
    public int ExpenseAccountId { get; set; }
    public string ExpenseAccountName { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public decimal PaidAmount { get; set; }
    public decimal RemainingAmount => Amount - PaidAmount;
    public string Description { get; set; } = string.Empty;
    public string? Reference { get; set; }
    public List<ProjectExpensePaymentDto> Payments { get; set; } = new();
}

public sealed class ProjectExpensePaymentDto
{
    public int Id { get; set; }
    public DateOnly Date { get; set; }
    public decimal Amount { get; set; }
    public string AccountName { get; set; } = string.Empty;
    public PaymentMethod PaymentMethod { get; set; }
    public string? Reference { get; set; }
    public string? Description { get; set; }
}

public interface IProjectExpenseService
{
    Task<List<ProjectExpenseDto>> GetListAsync(int tenantId, int? projectId = null);
    Task<ProjectExpenseDto?> GetByIdAsync(int id, int tenantId);
    Task<int> CreateAsync(ProjectExpenseRequest request, int tenantId);
    Task<int> AddPaymentAsync(ProjectExpensePaymentRequest request, int tenantId);
    Task DeletePaymentAsync(int expenseId, int paymentId, int tenantId);
    Task DeleteAsync(int expenseId, int tenantId);
}
