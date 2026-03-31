using System.Text.Json;

namespace TrustFirstPlatform.Application.Models
{
    public class ConsolidationResult
    {
        public bool Success { get; set; }
        public JsonDocument? ConsolidatedData { get; set; }
        public string? ErrorMessage { get; set; }
        public int RetryCount { get; set; }
        public bool HasConflicts { get; set; }
        public int DocumentsProcessed { get; set; }
        public List<string> ValidationErrors { get; set; } = new();
        public List<string> ValidationWarnings { get; set; } = new();
    }
}
