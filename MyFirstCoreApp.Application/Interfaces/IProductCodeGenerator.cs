namespace MyFirstCoreApp.Application.Interfaces;

public interface IProductCodeGenerator
{
    Task<string> GenerateNextCodeAsync(int tenantId);
}