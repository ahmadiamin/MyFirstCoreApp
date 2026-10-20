namespace MyFirstCoreApp.Models.ViewModels
{
    public class MenuTreeViewModel
    {
        public int Id { get; set; }

        public string Title { get; set; } = string.Empty;

        public string? Url { get; set; }

        public string? Icon { get; set; }

        public string? Permission { get; set; }

        public int SortOrder { get; set; }

        public bool IsActive { get; set; }

        public int Level { get; set; }

        public List<MenuTreeViewModel> Children { get; set; } = new();
    }
}