using System.ComponentModel.DataAnnotations;

namespace MyFirstCoreApp.Areas.Admin.ViewModels;

public sealed class SystemSmsSettingsViewModel
{
    public bool ApiKeyConfigured { get; set; }

    [DataType(DataType.Password)]
    public string? ApiKeyInput { get; set; }
    public bool ClearApiKey { get; set; }

    [Required]
    [StringLength(50)]
    public string LineNumber { get; set; } = "90008361";

    public bool RegistrationSmsEnabled { get; set; }

    [StringLength(100)]
    public string? RegistrationSmsPatternCode { get; set; }
}
