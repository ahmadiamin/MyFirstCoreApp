namespace MyFirstCoreApp.Areas.Tenancy.ViewModels;

public class SelectTenantViewModel
{
    public int TenantId { get; set; }
    public string TenantName { get; set; } = string.Empty;
    public bool IsOwner { get; set; }
    public bool IsDefault { get; set; }
}