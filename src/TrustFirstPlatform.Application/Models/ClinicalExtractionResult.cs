using System.Text.Json;

namespace TrustFirstPlatform.Application.Models
{
    public class ClinicalExtractionResult
    {
        public bool Success { get; set; }
        public JsonDocument? ExtractedData { get; set; }
        public string? ErrorMessage { get; set; }
        public int RetryCount { get; set; }
        public List<string> ValidationErrors { get; set; } = new();
        public List<string> ValidationWarnings { get; set; } = new();
        public bool HasValidationIssues => ValidationErrors.Any() || ValidationWarnings.Any();
    }
}
