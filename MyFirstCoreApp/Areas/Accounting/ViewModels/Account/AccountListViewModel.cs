namespace MyFirstCoreApp.Areas.Accounting.ViewModels.Account;

public class AccountListViewModel
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Code { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public string AccountTypeName { get; set; } = string.Empty;
    public string? BankName { get; set; }
    public string? AccountNumber { get; set; }
    public decimal CurrentBalance { get; set; }
    public bool IsActive { get; set; }
    public bool IsDefault { get; set; }
}