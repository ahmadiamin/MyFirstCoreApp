namespace MyFirstCoreApp.Application.Common.Interfaces;

public interface ICurrentUser
{
    string? UserId { get; }
    string? UserName { get; }
    bool IsAuthenticated { get; }
}