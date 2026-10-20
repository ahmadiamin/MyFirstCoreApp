namespace MyFirstCoreApp.Services.Interfaces
{
    public interface IFileService
    {
        Task<string> SaveAsync(
            IFormFile file,
            string folder);

        Task DeleteAsync(
            string relativePath);

        string GetPhysicalPath(
            string relativePath);

        string? GetExistingPhysicalPath(string relativePath);
    }
}