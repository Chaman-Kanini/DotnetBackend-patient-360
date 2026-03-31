using System;
using System.Text.Json;

namespace TrustFirstPlatform.Domain.Entities
{
    public class ClinicalDocument
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
        public Guid? PatientContextId { get; set; }
        public Guid? UploadBatchId { get; set; }

        public string OriginalFileName { get; set; } = string.Empty;
        public string StoredFileName { get; set; } = string.Empty;
        public string ContentType { get; set; } = string.Empty;
        public long FileSizeBytes { get; set; }
        public string FileExtension { get; set; } = string.Empty;
        public string StoragePath { get; set; } = string.Empty;
        public string FileHash { get; set; } = string.Empty;

        public DocumentStatus Status { get; set; }
        public string? ValidationError { get; set; }
        public string? ProcessingError { get; set; }
        public string? ExtractedText { get; set; }
        public JsonDocument? ExtractedData { get; set; }
        public DateTime UploadedAt { get; set; }
        public DateTime? ProcessedAt { get; set; }
        public JsonDocument Metadata { get; set; } = JsonDocument.Parse("{}");

        // Navigation properties
        public User User { get; set; } = null!;
        public PatientContext? PatientContext { get; set; }
        public UploadBatch? UploadBatch { get; set; }
    }
}
