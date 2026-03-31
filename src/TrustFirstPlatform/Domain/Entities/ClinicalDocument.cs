using System;
using System.Text.Json;

namespace TrustFirstPlatform.Domain.Entities
{
    public class ClinicalDocument
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
        public Guid? PatientContextId { get; set; }
        public string OriginalFileName { get; set; }
        public string StoredFileName { get; set; }
        public string ContentType { get; set; }
        public long FileSizeBytes { get; set; }
        public string FileExtension { get; set; }
        public string StoragePath { get; set; }
        public string FileHash { get; set; }
        public DocumentStatus Status { get; set; }
        public string? ValidationError { get; set; }
        public DateTime UploadedAt { get; set; }
        public DateTime? ProcessedAt { get; set; }
        public JsonDocument Metadata { get; set; }
        
        // Navigation properties
        public User User { get; set; }
        public PatientContext? PatientContext { get; set; }
    }
}
