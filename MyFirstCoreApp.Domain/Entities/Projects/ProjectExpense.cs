using MyFirstCoreApp.Domain.Common;
using MyFirstCoreApp.Domain.Entities.Accounting;
using MyFirstCoreApp.Domain.Entities.Contacts;

namespace MyFirstCoreApp.Domain.Entities.Projects;

public class ProjectExpense : TenantEntity
{
    public int Number { get; set; }
    public DateOnly Date { get; set; }
    public int? ProjectId { get; set; }
    public Project? Project { get; set; }
    public int? ContactId { get; set; }
    public Contact? Contact { get; set; }
    public int ExpenseAccountId { get; set; }
    public Account ExpenseAccount { get; set; } = null!;
    public decimal Amount { get; set; }
    public string Description { get; set; } = string.Empty;
    public string? Reference { get; set; }
    public bool IsPosted { get; set; }
    public int? TransactionId { get; set; }
    public Transaction? Transaction { get; set; }
    public ICollection<ProjectExpensePayment> Payments { get; set; } = new List<ProjectExpensePayment>();
}
