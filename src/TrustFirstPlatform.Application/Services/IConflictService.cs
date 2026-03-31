using System.Text.Json;
using TrustFirstPlatform.Application.Models;

namespace TrustFirstPlatform.Application.Services
{
    public interface IConflictService
    {
        ConflictSection? ParseConflicts(JsonDocument? consolidatedData);
        
        int CountConflicts(ConflictSection? conflictSection);
        
        ConflictSummary GetConflictSummary(ConflictSection? conflictSection);
        
        List<ConflictEntry> GetConflictsByCategory(ConflictSection? conflictSection, string category);
        
        List<ConflictEntry> GetConflictsBySeverity(ConflictSection? conflictSection, ConflictSeverity severity);
    }
}
