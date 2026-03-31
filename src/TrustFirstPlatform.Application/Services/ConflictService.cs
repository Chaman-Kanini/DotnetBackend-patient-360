using Microsoft.Extensions.Logging;
using System.Text.Json;
using TrustFirstPlatform.Application.Models;

namespace TrustFirstPlatform.Application.Services
{
    public class ConflictService : IConflictService
    {
        private readonly ILogger<ConflictService> _logger;

        public ConflictService(ILogger<ConflictService> logger)
        {
            _logger = logger;
        }

        public ConflictSection? ParseConflicts(JsonDocument? consolidatedData)
        {
            if (consolidatedData == null)
            {
                _logger.LogDebug("No consolidated data provided for conflict parsing");
                return null;
            }

            try
            {
                var root = consolidatedData.RootElement;
                
                if (!root.TryGetProperty("Conflicts", out var conflictsProperty))
                {
                    _logger.LogDebug("No Conflicts section found in consolidated data");
                    return null;
                }

                if (conflictsProperty.ValueKind != JsonValueKind.Object)
                {
                    _logger.LogWarning("Conflicts section is not a valid JSON object");
                    return null;
                }

                var conflictSection = JsonSerializer.Deserialize<ConflictSection>(
                    conflictsProperty.GetRawText(),
                    new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                if (conflictSection != null)
                {
                    var totalConflicts = CountConflicts(conflictSection);
                    _logger.LogInformation(
                        "Successfully parsed {TotalConflicts} conflicts from consolidated data",
                        totalConflicts);
                }

                return conflictSection;
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Failed to parse Conflicts section from consolidated data");
                return null;
            }
        }

        public int CountConflicts(ConflictSection? conflictSection)
        {
            if (conflictSection == null)
            {
                return 0;
            }

            var count = 0;
            count += conflictSection.Medications?.Count ?? 0;
            count += conflictSection.Demographics?.Count ?? 0;
            count += conflictSection.Allergies?.Count ?? 0;
            count += conflictSection.Vitals?.Count ?? 0;
            count += conflictSection.SocialHistory?.Count ?? 0;
            count += conflictSection.Procedures?.Count ?? 0;
            count += conflictSection.LabResults?.Count ?? 0;
            count += conflictSection.Immunizations?.Count ?? 0;

            return count;
        }

        public ConflictSummary GetConflictSummary(ConflictSection? conflictSection)
        {
            var summary = new ConflictSummary
            {
                TotalConflicts = CountConflicts(conflictSection)
            };

            if (conflictSection == null || summary.TotalConflicts == 0)
            {
                return summary;
            }

            var allConflicts = GetAllConflicts(conflictSection);

            summary.CriticalConflicts = allConflicts.Count(c => c.Severity == ConflictSeverity.Critical);
            summary.WarningConflicts = allConflicts.Count(c => c.Severity == ConflictSeverity.Warning);
            summary.InfoConflicts = allConflicts.Count(c => c.Severity == ConflictSeverity.Info);

            summary.ConflictsByCategory = new Dictionary<string, int>
            {
                ["Medications"] = conflictSection.Medications?.Count ?? 0,
                ["Demographics"] = conflictSection.Demographics?.Count ?? 0,
                ["Allergies"] = conflictSection.Allergies?.Count ?? 0,
                ["Vitals"] = conflictSection.Vitals?.Count ?? 0,
                ["SocialHistory"] = conflictSection.SocialHistory?.Count ?? 0,
                ["Procedures"] = conflictSection.Procedures?.Count ?? 0,
                ["LabResults"] = conflictSection.LabResults?.Count ?? 0,
                ["Immunizations"] = conflictSection.Immunizations?.Count ?? 0
            };

            _logger.LogInformation(
                "Conflict summary: Total={Total}, Critical={Critical}, Warning={Warning}, Info={Info}",
                summary.TotalConflicts,
                summary.CriticalConflicts,
                summary.WarningConflicts,
                summary.InfoConflicts);

            return summary;
        }

        public List<ConflictEntry> GetConflictsByCategory(ConflictSection? conflictSection, string category)
        {
            if (conflictSection == null || string.IsNullOrWhiteSpace(category))
            {
                return new List<ConflictEntry>();
            }

            return category.ToLowerInvariant() switch
            {
                "medications" => conflictSection.Medications ?? new List<ConflictEntry>(),
                "demographics" => conflictSection.Demographics ?? new List<ConflictEntry>(),
                "allergies" => conflictSection.Allergies ?? new List<ConflictEntry>(),
                "vitals" => conflictSection.Vitals ?? new List<ConflictEntry>(),
                "socialhistory" => conflictSection.SocialHistory ?? new List<ConflictEntry>(),
                "procedures" => conflictSection.Procedures ?? new List<ConflictEntry>(),
                "labresults" => conflictSection.LabResults ?? new List<ConflictEntry>(),
                "immunizations" => conflictSection.Immunizations ?? new List<ConflictEntry>(),
                _ => new List<ConflictEntry>()
            };
        }

        public List<ConflictEntry> GetConflictsBySeverity(ConflictSection? conflictSection, ConflictSeverity severity)
        {
            if (conflictSection == null)
            {
                return new List<ConflictEntry>();
            }

            var allConflicts = GetAllConflicts(conflictSection);
            return allConflicts.Where(c => c.Severity == severity).ToList();
        }

        private List<ConflictEntry> GetAllConflicts(ConflictSection conflictSection)
        {
            var allConflicts = new List<ConflictEntry>();

            if (conflictSection.Medications != null)
                allConflicts.AddRange(conflictSection.Medications);
            
            if (conflictSection.Demographics != null)
                allConflicts.AddRange(conflictSection.Demographics);
            
            if (conflictSection.Allergies != null)
                allConflicts.AddRange(conflictSection.Allergies);
            
            if (conflictSection.Vitals != null)
                allConflicts.AddRange(conflictSection.Vitals);
            
            if (conflictSection.SocialHistory != null)
                allConflicts.AddRange(conflictSection.SocialHistory);
            
            if (conflictSection.Procedures != null)
                allConflicts.AddRange(conflictSection.Procedures);
            
            if (conflictSection.LabResults != null)
                allConflicts.AddRange(conflictSection.LabResults);
            
            if (conflictSection.Immunizations != null)
                allConflicts.AddRange(conflictSection.Immunizations);

            return allConflicts;
        }
    }
}
