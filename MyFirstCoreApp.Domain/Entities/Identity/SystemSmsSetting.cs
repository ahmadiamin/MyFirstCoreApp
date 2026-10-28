using MyFirstCoreApp.Domain.Common;

namespace MyFirstCoreApp.Domain.Entities.Identity;

public sealed class SystemSmsSetting : BaseEntity
{
    public string? ProtectedApiKey { get; set; }
    public string LineNumber { get; set; } = "90008361";
    public bool RegistrationSmsEnabled { get; set; }
    public string? RegistrationSmsPatternCode { get; set; }
}
