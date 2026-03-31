using TrustFirstPlatform.Application.Models;

namespace TrustFirstPlatform.Application.Services
{
    public interface IClinicalExtractionService
    {
        Task<ClinicalExtractionResult> ExtractClinicalDataAsync(string extractedText, CancellationToken cancellationToken = default);
    }
}
