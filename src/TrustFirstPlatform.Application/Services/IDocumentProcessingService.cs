namespace TrustFirstPlatform.Application.Services
{
    public interface IDocumentProcessingService
    {
        Task<ProcessingResult> ProcessDocumentAsync(Guid documentId, Guid userId);
        Task<ProcessingResult> ConsolidateAllUserDocumentsAsync(Guid userId);
    }

    public class ProcessingResult
    {
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
        public string? ExtractedText { get; set; }
        public TimeSpan ProcessingDuration { get; set; }
    }
}
