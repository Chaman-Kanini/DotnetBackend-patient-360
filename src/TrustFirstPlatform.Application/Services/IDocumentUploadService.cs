using Microsoft.AspNetCore.Http;
using System.IO;
using TrustFirstPlatform.Domain.Entities;

namespace TrustFirstPlatform.Application.Services
{
    public interface IDocumentUploadService
    {
        Task<UploadResult> UploadDocumentAsync(IFormFile file, Guid userId, Guid? patientContextId, bool skipProcessing = false);
        Task<BatchUploadResult> UploadDocumentsAsync(IFormFileCollection files, Guid userId, Guid? patientContextId);
        Task<ClinicalDocument?> GetDocumentAsync(Guid documentId, Guid userId);
        Task<IEnumerable<ClinicalDocument>> GetUserDocumentsAsync(Guid userId);
        Task<DocumentFileResult?> GetDocumentFileAsync(Guid documentId, Guid userId);
        Task<DeleteDocumentResult> DeleteDocumentAsync(Guid documentId, Guid userId);
    }

    public class UploadResult
    {
        public bool Success { get; set; }
        public ClinicalDocument? Document { get; set; }
        public string? OriginalFileName { get; set; }
        public string? ErrorMessage { get; set; }
        public ValidationErrorType? ErrorType { get; set; }
    }

    public class BatchUploadResult
    {
        public List<UploadResult> Results { get; set; } = new();
        public int SuccessCount => Results.Count(r => r.Success);
        public int FailureCount => Results.Count(r => !r.Success);
        public bool HasAnySuccess => SuccessCount > 0;
    }

    public class DocumentFileResult
    {
        public Stream Stream { get; set; } = null!;
        public string FileName { get; set; } = string.Empty;
        public string ContentType { get; set; } = "application/octet-stream";
    }

    public class DeleteDocumentResult
    {
        public bool Success { get; set; }
        public bool NotFound { get; set; }
        public string? ErrorMessage { get; set; }
    }
}
