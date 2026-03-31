using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using TrustFirstPlatform.Application.Models;

namespace TrustFirstPlatform.Application.Services
{
    public interface IMedicalCodeLookupService
    {
        Task<string?> GetICD10CodeAsync(string diagnosis);
        Task<string?> GetCPTCodeAsync(string procedure);
        Task<Dictionary<string, string>> GetICD10CodesAsync(List<string> diagnoses);
        Task<Dictionary<string, string>> GetCPTCodesAsync(List<string> procedures);
        Task<MedicalCodesResult> EnrichConsolidatedDataWithCodesAsync(JsonDocument consolidatedData);
    }
}
