using Microsoft.AspNetCore.Identity;
using MyFirstCoreApp.Domain.Entities.Identity;

namespace MyFirstCoreApp.Services;

public sealed class ApplicationUserConfirmation : IUserConfirmation<ApplicationUser>
{
    public Task<bool> IsConfirmedAsync(UserManager<ApplicationUser> manager, ApplicationUser user)
        => Task.FromResult(user.EmailConfirmed || user.PhoneNumberConfirmed);
}
