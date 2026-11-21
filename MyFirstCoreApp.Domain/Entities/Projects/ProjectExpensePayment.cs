using MyFirstCoreApp.Domain.Common;
using MyFirstCoreApp.Domain.Entities.Accounting;
using MyFirstCoreApp.Domain.Enums;

namespace MyFirstCoreApp.Domain.Entities.Projects;

public class ProjectExpensePayment : TenantEntity
{
    public int ProjectExpenseId { get; set; }
    public ProjectExpense ProjectExpense { get; set; } = null!;
    public DateOnly Date { get; set; }
    public decimal Amount { get; set; }
    public int AccountId { get; set; }
    public Account Account { get; set; } = null!;
    public PaymentMethod PaymentMethod { get; set; }
    public string? Reference { get; set; }
    public string? Description { get; set; }
    public int TransactionId { get; set; }
    public Transaction Transaction { get; set; } = null!;
}
