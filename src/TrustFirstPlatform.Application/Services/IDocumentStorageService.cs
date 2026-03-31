using Microsoft.AspNetCore.Http;

namespace TrustFirstPlatform.Application.Services
{
    public interface IDocumentStorageService
    {
        Task<StorageResult> SaveFileAsync(IFormFile file, Guid userId);
        Task<Stream> GetFileAsync(string storagePath);
        Task<bool> DeleteFileAsync(string storagePath);
        string GenerateSecureFileName(string originalFileName);
        Task<string> CalculateFileHashAsync(Stream fileStream);
    }

    public class StorageResult
    {
        public bool Success { get; set; }
        public string? StoragePath { get; set; }
        public string? StoredFileName { get; set; }
        public string? FileHash { get; set; }
        public string? ErrorMessage { get; set; }
    }
}
