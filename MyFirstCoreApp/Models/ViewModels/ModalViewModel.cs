namespace MyFirstCoreApp.Models.ViewModels;

public class ModalViewModel
{
    public string Id { get; set; } = "generalModal";
    public string Title { get; set; } = "عنوان";
    public string? Icon { get; set; } = "fa-solid fa-info-circle";
    public string HeaderClass { get; set; } = "bg-primary text-white";
    public string Size { get; set; } = "modal-md";   // modal-sm, modal-md, modal-lg, modal-xl
    public bool Centered { get; set; } = true;
    public bool IsLoading { get; set; } = false;
    public string? BodyHtml { get; set; }
    public List<ModalButton> Buttons { get; set; } = new();
}

public class ModalButton
{
    public string Id { get; set; } = "";
    public string Text { get; set; } = "دکمه";
    public string Class { get; set; } = "btn-secondary";
    public string? Icon { get; set; }
    public string? Action { get; set; }   // "submit", "cancel", "confirm"
    public bool DismissOnClick { get; set; } = false;
}