using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography;
using System.Text;

namespace TrustFirstPlatform.Application.Services
{
    public class DocumentStorageService : IDocumentStorageService
    {
        private readonly string _baseStoragePath;
        private readonly ILogger<DocumentStorageService> _logger;

        public DocumentStorageService(IConfiguration configuration, ILogger<DocumentStorageService> logger)
        {
            _baseStoragePath = configuration["DocumentStorage:BasePath"] ?? 
                Path.Combine(Directory.GetCurrentDirectory(), "uploads");
            _logger = logger;
            
            // Ensure base directory exists
            Directory.CreateDirectory(_baseStoragePath);
        }

        public async Task<StorageResult> SaveFileAsync(IFormFile file, Guid userId)
        {
            try
            {
                var storedFileName = GenerateSecureFileName(file.FileName);
                var relativePath = GetRelativeStoragePath(userId, storedFileName);
                var fullPath = Path.Combine(_baseStoragePath, relativePath);
                
                // Ensure directory exists
                var directory = Path.GetDirectoryName(fullPath);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                // Calculate hash before saving
                using var stream = file.OpenReadStream();
                var fileHash = await CalculateFileHashAsync(stream);
                stream.Position = 0;

                // Save file
                using var fileStream = new FileStream(fullPath, FileMode.Create, FileAccess.Write);
                await stream.CopyToAsync(fileStream);

                return new StorageResult
                {
                    Success = true,
                    StoragePath = relativePath,
                    StoredFileName = storedFileName,
                    FileHash = fileHash
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving file {FileName} for user {UserId}", file.FileName, userId);
                return new StorageResult
                {
                    Success = false,
                    ErrorMessage = $"Failed to save file: {ex.Message}"
                };
            }
        }

        public async Task<Stream> GetFileAsync(string storagePath)
        {
            try
            {
                var fullPath = Path.Combine(_baseStoragePath, storagePath);
                
                if (!File.Exists(fullPath))
                {
                    throw new FileNotFoundException("File not found", storagePath);
                }

                return new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving file {StoragePath}", storagePath);
                throw;
            }
        }

        public async Task<bool> DeleteFileAsync(string storagePath)
        {
            try
            {
                var fullPath = Path.Combine(_baseStoragePath, storagePath);
                
                if (File.Exists(fullPath))
                {
                    File.Delete(fullPath);
                    _logger.LogInformation("Successfully deleted file {StoragePath}", storagePath);
                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting file {StoragePath}", storagePath);
                return false;
            }
        }

        public string GenerateSecureFileName(string originalFileName)
        {
            var extension = Path.GetExtension(originalFileName);
            var guid = Guid.NewGuid().ToString("N");
            return $"{guid}{extension}";
        }

        public async Task<string> CalculateFileHashAsync(Stream fileStream)
        {
            using var sha256 = SHA256.Create();
            var hash = await sha256.ComputeHashAsync(fileStream);
            return Convert.ToHexString(hash).ToLowerInvariant();
        }

        private string GetRelativeStoragePath(Guid userId, string storedFileName)
        {
            var now = DateTime.UtcNow;
            var year = now.ToString("yyyy");
            var month = now.ToString("MM");
            var day = now.ToString("dd");
            
            return Path.Combine(year, month, day, userId.ToString(), storedFileName);
        }
    }
}
