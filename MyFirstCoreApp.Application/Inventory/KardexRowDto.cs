namespace MyFirstCoreApp.Application.Inventory;

public class KardexRowDto
{
    public DateOnly Date { get; set; }

    public string DocumentNumber { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public decimal Incoming { get; set; }

    public decimal Outgoing { get; set; }

    public decimal Balance { get; set; }
}