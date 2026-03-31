using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TrustFirstPlatform.Domain.Entities;
using TrustFirstPlatform.Infrastructure.Data;

namespace TrustFirstPlatform.Application.Services
{
    public class BatchProcessingService : IBatchProcessingService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<BatchProcessingService> _logger;

        public BatchProcessingService(
            IServiceScopeFactory scopeFactory,
            ILogger<BatchProcessingService> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        public async Task ProcessBatchAsync(Guid batchId, Guid userId, List<Guid> documentIds)
        {
            try
            {
                _logger.LogInformation("Starting batch processing for batch {BatchId} with {DocumentCount} documents", 
                    batchId, documentIds.Count);

                // STEP 1: Process all documents in PARALLEL
                var tasks = documentIds.Select(docId => ProcessSingleDocumentAsync(docId, userId));
                await Task.WhenAll(tasks);

                _logger.LogInformation("All documents processed for batch {BatchId}", batchId);

                // STEP 2: Check if all completed successfully
                using var scope = _scopeFactory.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                var documents = await dbContext.ClinicalDocuments
                    .Where(d => d.UploadBatchId == batchId)
                    .ToListAsync();

                var completedCount = documents.Count(d => d.Status == DocumentStatus.Completed);
                var failedCount = documents.Count(d => d.Status == DocumentStatus.Failed);

                _logger.LogInformation(
                    "Batch {BatchId} processing summary: {CompletedCount} completed, {FailedCount} failed out of {TotalCount}",
                    batchId, completedCount, failedCount, documents.Count);

                // Update batch processed count
                var batch = await dbContext.UploadBatches.FindAsync(batchId);
                if (batch != null)
                {
                    batch.ProcessedDocuments = completedCount + failedCount;
                    await dbContext.SaveChangesAsync();
                }

                // STEP 3: Consolidate if we have at least 1 completed document
                if (completedCount >= 1)
                {
                    _logger.LogInformation("Starting consolidation for batch {BatchId} with {CompletedCount} documents", 
                        batchId, completedCount);

                    var consolidationService = scope.ServiceProvider.GetRequiredService<IConsolidationService>();
                    var auditService = scope.ServiceProvider.GetRequiredService<IAuditService>();

                    var patientContext = new PatientContext
                    {
                        Id = Guid.NewGuid(),
                        PatientIdentifier = $"PAT-{DateTime.UtcNow:yyyyMMdd-HHmmss}-{Guid.NewGuid().ToString("N")[..8].ToUpper()}",
                        PatientName = $"Batch Upload - {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}",
                        CreatedByUserId = userId,
                        CreatedAt = DateTime.UtcNow,
                        Status = "Active"
                    };

                    dbContext.PatientContexts.Add(patientContext);

                    // Link ALL completed documents to this context
                    var completedDocs = documents.Where(d => d.Status == DocumentStatus.Completed).ToList();
                    foreach (var doc in completedDocs)
                    {
                        doc.PatientContextId = patientContext.Id;
                    }

                    await dbContext.SaveChangesAsync();

                    _logger.LogInformation("Created PatientContext {PatientContextId} and linked {DocumentCount} documents",
                        patientContext.Id, completedDocs.Count);

                    // Consolidate
                    var consolidationResult = await consolidationService.ConsolidatePatientDataAsync(patientContext.Id);

                    if (consolidationResult.Success)
                    {
                        _logger.LogInformation(
                            "Consolidation completed successfully for batch {BatchId}, PatientContext {PatientContextId}",
                            batchId, patientContext.Id);

                        await auditService.LogAsync(userId, "BATCH_CONSOLIDATION_COMPLETED", new
                        {
                            BatchId = batchId,
                            PatientContextId = patientContext.Id,
                            DocumentsProcessed = consolidationResult.DocumentsProcessed,
                            HasConflicts = consolidationResult.HasConflicts
                        });
                    }
                    else
                    {
                        _logger.LogWarning(
                            "Consolidation failed for batch {BatchId}: {Error}",
                            batchId, consolidationResult.ErrorMessage);

                        await auditService.LogAsync(userId, "BATCH_CONSOLIDATION_FAILED", new
                        {
                            BatchId = batchId,
                            PatientContextId = patientContext.Id,
                            ErrorMessage = consolidationResult.ErrorMessage
                        });
                    }
                }
                else
                {
                    _logger.LogInformation(
                        "Skipping consolidation for batch {BatchId} - only {CompletedCount} completed documents",
                        batchId, completedCount);
                }

                // Update batch status
                if (batch != null)
                {
                    batch.Status = failedCount == documents.Count ? "Failed" : "Completed";
                    await dbContext.SaveChangesAsync();
                }

                _logger.LogInformation("Batch processing completed for batch {BatchId}", batchId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during batch processing for batch {BatchId}", batchId);

                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                    var batch = await dbContext.UploadBatches.FindAsync(batchId);
                    if (batch != null)
                    {
                        batch.Status = "Failed";
                        await dbContext.SaveChangesAsync();
                    }
                }
                catch (Exception innerEx)
                {
                    _logger.LogError(innerEx, "Failed to update batch status for batch {BatchId}", batchId);
                }
            }
        }

        private async Task ProcessSingleDocumentAsync(Guid docId, Guid userId)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var processingService = scope.ServiceProvider.GetRequiredService<IDocumentProcessingService>();

                // Process document (no automatic consolidation per document)
                await processingService.ProcessDocumentAsync(docId, userId);

                _logger.LogInformation("Document {DocumentId} processed successfully", docId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing document {DocumentId}", docId);
            }
        }
    }
}
