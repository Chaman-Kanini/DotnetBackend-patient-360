using TrustFirstPlatform.Application.Models;

namespace TrustFirstPlatform.Application.Services
{
    public interface IConsolidationService
    {
        Task<ConsolidationResult> ConsolidatePatientDataAsync(Guid patientContextId, CancellationToken cancellationToken = default);
    }
}
