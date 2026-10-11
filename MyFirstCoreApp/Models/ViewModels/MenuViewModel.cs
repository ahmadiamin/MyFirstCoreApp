namespace MyFirstCoreApp.Models.ViewModels
{
    public class MenuViewModel
    {
        public int Id { get; set; }

        public string Title { get; set; } = string.Empty;

        public string? Url { get; set; }

        public string? Icon { get; set; }

        public List<MenuViewModel> Children { get; set; } = new();
    }
}