namespace TrustFirstPlatform.Application.Services
{
    public interface IBatchProcessingService
    {
        Task ProcessBatchAsync(Guid batchId, Guid userId, List<Guid> documentIds);
    }
}
