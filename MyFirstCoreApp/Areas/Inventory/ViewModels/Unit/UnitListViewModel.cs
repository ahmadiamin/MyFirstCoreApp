namespace MyFirstCoreApp.Areas.Inventory.ViewModels.Unit;

public class UnitListViewModel
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Symbol { get; set; }
    public int SortOrder { get; set; } 
    public bool IsActive { get; set; }
}