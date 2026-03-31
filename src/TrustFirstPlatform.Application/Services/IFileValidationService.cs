using Microsoft.AspNetCore.Http;

namespace TrustFirstPlatform.Application.Services
{
    public interface IFileValidationService
    {
        Task<FileValidationResult> ValidateFileAsync(IFormFile file);
        bool IsAllowedExtension(string extension);
        bool IsWithinSizeLimit(long fileSize);
        Task<bool> IsPasswordProtectedPdfAsync(Stream fileStream);
        Task<bool> IsCorruptedFileAsync(Stream fileStream, string contentType);
        bool IsValidMimeType(string contentType, string extension);
    }

    public class FileValidationResult
    {
        public bool IsValid { get; set; }
        public string? ErrorMessage { get; set; }
        public ValidationErrorType? ErrorType { get; set; }
    }

    public enum ValidationErrorType
    {
        InvalidExtension,
        FileTooLarge,
        InvalidMimeType,
        PasswordProtected,
        CorruptedFile,
        EmptyFile
    }
}
