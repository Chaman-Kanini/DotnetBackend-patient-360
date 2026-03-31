using System.Text.Json.Serialization;

namespace TrustFirstPlatform.Application.Models
{
    public class ConflictSection
    {
        [JsonPropertyName("Medications")]
        public List<ConflictEntry> Medications { get; set; } = new();

        [JsonPropertyName("Demographics")]
        public List<ConflictEntry> Demographics { get; set; } = new();

        [JsonPropertyName("Allergies")]
        public List<ConflictEntry> Allergies { get; set; } = new();

        [JsonPropertyName("Vitals")]
        public List<ConflictEntry> Vitals { get; set; } = new();

        [JsonPropertyName("SocialHistory")]
        public List<ConflictEntry> SocialHistory { get; set; } = new();

        [JsonPropertyName("Procedures")]
        public List<ConflictEntry> Procedures { get; set; } = new();

        [JsonPropertyName("LabResults")]
        public List<ConflictEntry> LabResults { get; set; } = new();

        [JsonPropertyName("Immunizations")]
        public List<ConflictEntry> Immunizations { get; set; } = new();
    }

    public class ConflictEntry
    {
        [JsonPropertyName("Entity")]
        public string Entity { get; set; } = string.Empty;

        [JsonPropertyName("Conflict_Type")]
        public string ConflictType { get; set; } = string.Empty;

        [JsonPropertyName("Variants")]
        public List<ConflictVariant> Variants { get; set; } = new();

        [JsonIgnore]
        public ConflictSeverity Severity
        {
            get
            {
                var entityLower = Entity.ToLowerInvariant();
                var conflictTypeLower = ConflictType.ToLowerInvariant();

                if (conflictTypeLower.Contains("allergy") || 
                    conflictTypeLower.Contains("medication dosage") ||
                    conflictTypeLower.Contains("dob") ||
                    conflictTypeLower.Contains("gender") ||
                    entityLower.Contains("allergy"))
                {
                    return ConflictSeverity.Critical;
                }

                if (conflictTypeLower.Contains("vital") ||
                    conflictTypeLower.Contains("smoking") ||
                    conflictTypeLower.Contains("social history"))
                {
                    return ConflictSeverity.Warning;
                }

                return ConflictSeverity.Info;
            }
        }
    }

    public class ConflictVariant
    {
        [JsonPropertyName("_source")]
        public string Source { get; set; } = string.Empty;

        [JsonExtensionData]
        public Dictionary<string, object>? AdditionalFields { get; set; }
    }

    public enum ConflictSeverity
    {
        Info,
        Warning,
        Critical
    }

    public class ConflictSummary
    {
        public int TotalConflicts { get; set; }
        public int CriticalConflicts { get; set; }
        public int WarningConflicts { get; set; }
        public int InfoConflicts { get; set; }
        public Dictionary<string, int> ConflictsByCategory { get; set; } = new();
    }
}
