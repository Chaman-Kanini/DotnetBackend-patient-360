using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using TrustFirstPlatform.Domain.Entities;
using TrustFirstPlatform.Infrastructure.Data;

namespace TrustFirstPlatform.Application.Services
{
    public class DocumentProcessingService : IDocumentProcessingService
    {
        private readonly AppDbContext _dbContext;
        private readonly IPythonTextExtractionService _pythonTextExtractionService;
        private readonly IClinicalExtractionService _clinicalExtractionService;
        private readonly IAuditService _auditService;
        private readonly IDocumentStorageService _documentStorageService;
        private readonly IConsolidationService _consolidationService;
        private readonly IConfiguration _configuration;
        private readonly ILogger<DocumentProcessingService> _logger;
        private const int ProcessingTimeoutSeconds = 600;

        public DocumentProcessingService(
            AppDbContext dbContext,
            IPythonTextExtractionService pythonTextExtractionService,
            IClinicalExtractionService clinicalExtractionService,
            IAuditService auditService,
            IDocumentStorageService documentStorageService,
            IConsolidationService consolidationService,
            IConfiguration configuration,
            ILogger<DocumentProcessingService> logger)
        {
            _dbContext = dbContext;
            _pythonTextExtractionService = pythonTextExtractionService;
            _clinicalExtractionService = clinicalExtractionService;
            _auditService = auditService;
            _documentStorageService = documentStorageService;
            _consolidationService = consolidationService;
            _configuration = configuration;
            _logger = logger;
        }

        public async Task<ProcessingResult> ProcessDocumentAsync(Guid documentId, Guid userId)
        {
            var stopwatch = Stopwatch.StartNew();
            
            try
            {
                var document = await _dbContext.ClinicalDocuments
                    .FirstOrDefaultAsync(d => d.Id == documentId && d.UserId == userId);

                if (document == null)
                {
                    _logger.LogWarning("Document {DocumentId} not found for user {UserId}", documentId, userId);
                    return new ProcessingResult
                    {
                        Success = false,
                        ErrorMessage = "Document not found",
                        ProcessingDuration = stopwatch.Elapsed
                    };
                }

                if (document.Status != DocumentStatus.Validated)
                {
                    _logger.LogWarning("Document {DocumentId} is not in Validated status. Current status: {Status}", 
                        documentId, document.Status);
                    return new ProcessingResult
                    {
                        Success = false,
                        ErrorMessage = $"Document is not in Validated status. Current status: {document.Status}",
                        ProcessingDuration = stopwatch.Elapsed
                    };
                }

                await TransitionToProcessingAsync(document, userId);

                var extractionResult = await ExtractTextWithTimeoutAsync(document.StoragePath);

                if (!string.IsNullOrEmpty(extractionResult.Error))
                {
                    await TransitionToFailedAsync(document, userId, extractionResult.Error, stopwatch.Elapsed);
                    return new ProcessingResult
                    {
                        Success = false,
                        ErrorMessage = extractionResult.Error,
                        ProcessingDuration = stopwatch.Elapsed
                    };
                }

                if (string.IsNullOrWhiteSpace(extractionResult.Text))
                {
                    var errorMessage = "Extracted text is empty. The document may be image-only or corrupted.";
                    await TransitionToFailedAsync(document, userId, errorMessage, stopwatch.Elapsed);
                    return new ProcessingResult
                    {
                        Success = false,
                        ErrorMessage = errorMessage,
                        ProcessingDuration = stopwatch.Elapsed
                    };
                }

                // Extract clinical data using Azure OpenAI
                _logger.LogInformation(
                    "Starting clinical data extraction for document {DocumentId} with {TextLength} characters",
                    documentId, extractionResult.Text.Length);

                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(ProcessingTimeoutSeconds));
                var clinicalResult = await _clinicalExtractionService.ExtractClinicalDataAsync(
                    extractionResult.Text, cts.Token);

                if (!clinicalResult.Success)
                {
                    _logger.LogError(
                        "Clinical extraction failed for document {DocumentId}: {ErrorMessage}. Validation errors: {ValidationErrorCount}",
                        documentId, clinicalResult.ErrorMessage, clinicalResult.ValidationErrors.Count);
                    
                    await TransitionToFailedAsync(document, userId, 
                        clinicalResult.ErrorMessage ?? "Clinical extraction failed", stopwatch.Elapsed);
                    return new ProcessingResult
                    {
                        Success = false,
                        ErrorMessage = clinicalResult.ErrorMessage,
                        ProcessingDuration = stopwatch.Elapsed
                    };
                }

                if (clinicalResult.HasValidationIssues)
                {
                    _logger.LogWarning(
                        "Clinical extraction completed for document {DocumentId} with {ErrorCount} errors and {WarningCount} warnings",
                        documentId, clinicalResult.ValidationErrors.Count, clinicalResult.ValidationWarnings.Count);
                }
                else
                {
                    _logger.LogInformation(
                        "Clinical extraction completed successfully for document {DocumentId} with no validation issues",
                        documentId);
                }

                await TransitionToCompletedAsync(document, userId, extractionResult.Text, 
                    clinicalResult.ExtractedData, stopwatch.Elapsed);

                // Do NOT trigger automatic consolidation - user must explicitly call consolidation
                // This ensures individual document extractions stay in ClinicalDocuments.ExtractedData
                // and only the merged result goes to PatientContexts.ConsolidatedData

                return new ProcessingResult
                {
                    Success = true,
                    ExtractedText = extractionResult.Text,
                    ProcessingDuration = stopwatch.Elapsed
                };
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                _logger.LogError(ex, "Unexpected error processing document {DocumentId} for user {UserId}", 
                    documentId, userId);

                try
                {
                    var document = await _dbContext.ClinicalDocuments
                        .FirstOrDefaultAsync(d => d.Id == documentId);
                    
                    if (document != null)
                    {
                        await TransitionToFailedAsync(document, userId, 
                            "An unexpected error occurred during processing", stopwatch.Elapsed);
                    }
                }
                catch (Exception innerEx)
                {
                    _logger.LogError(innerEx, "Failed to update document status after processing error for document {DocumentId}", 
                        documentId);
                }

                return new ProcessingResult
                {
                    Success = false,
                    ErrorMessage = "An unexpected error occurred during processing",
                    ProcessingDuration = stopwatch.Elapsed
                };
            }
        }

        private async Task<TextExtractionResult> ExtractTextWithTimeoutAsync(string filePath)
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(ProcessingTimeoutSeconds));
            
            try
            {
                // Resolve full file path using storage service configuration
                var fullPath = GetFullFilePath(filePath);
                _logger.LogInformation("Resolved file path: {RelativePath} -> {FullPath}", filePath, fullPath);
                
                var extractionTask = _pythonTextExtractionService.ExtractTextAsync(fullPath);
                var completedTask = await Task.WhenAny(extractionTask, Task.Delay(Timeout.Infinite, cts.Token));

                if (completedTask == extractionTask)
                {
                    return await extractionTask;
                }
                else
                {
                    _logger.LogError("Text extraction timed out after {TimeoutSeconds} seconds for file {FilePath}", 
                        ProcessingTimeoutSeconds, filePath);
                    return new TextExtractionResult 
                    { 
                        Error = $"Processing timed out after {ProcessingTimeoutSeconds} seconds" 
                    };
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogError("Text extraction was cancelled for file {FilePath}", filePath);
                return new TextExtractionResult { Error = "Processing was cancelled" };
            }
        }

        private async Task TransitionToProcessingAsync(ClinicalDocument document, Guid userId)
        {
            document.Status = DocumentStatus.Processing;
            document.ValidationError = null;
            document.ProcessingError = null;
            
            await _dbContext.SaveChangesAsync();

            _logger.LogInformation("Document {DocumentId} transitioned to Processing status", document.Id);

            await _auditService.LogAsync(userId, "DOCUMENT_PROCESSING_STARTED", new
            {
                DocumentId = document.Id,
                FileName = document.OriginalFileName,
                FileSize = document.FileSizeBytes
            });
        }

        private async Task TransitionToCompletedAsync(ClinicalDocument document, Guid userId, 
            string extractedText, System.Text.Json.JsonDocument? extractedData, TimeSpan processingDuration)
        {
            document.Status = DocumentStatus.Completed;
            document.ProcessedAt = DateTime.UtcNow;
            document.ValidationError = null;
            document.ProcessingError = null;
            document.ExtractedText = extractedText;
            document.ExtractedData = extractedData;

            await _dbContext.SaveChangesAsync();

            _logger.LogInformation(
                "Document {DocumentId} processed successfully in {DurationMs}ms. Extracted {TextLength} characters",
                document.Id, processingDuration.TotalMilliseconds, extractedText.Length);

            await _auditService.LogAsync(userId, "DOCUMENT_PROCESSING_COMPLETED", new
            {
                DocumentId = document.Id,
                FileName = document.OriginalFileName,
                ProcessingDurationMs = processingDuration.TotalMilliseconds,
                ExtractedTextLength = extractedText.Length,
                HasExtractedData = extractedData != null,
                ExtractedDataSize = extractedData?.RootElement.GetRawText().Length ?? 0
            });
        }

        private async Task TransitionToFailedAsync(ClinicalDocument document, Guid userId, 
            string errorMessage, TimeSpan processingDuration)
        {
            document.Status = DocumentStatus.Failed;
            document.ProcessedAt = DateTime.UtcNow;
            document.ProcessingError = errorMessage;

            await _dbContext.SaveChangesAsync();

            _logger.LogWarning(
                "Document {DocumentId} processing failed after {DurationMs}ms. Error: {ErrorMessage}",
                document.Id, processingDuration.TotalMilliseconds, errorMessage);

            await _auditService.LogAsync(userId, "DOCUMENT_PROCESSING_FAILED", new
            {
                DocumentId = document.Id,
                FileName = document.OriginalFileName,
                ProcessingDurationMs = processingDuration.TotalMilliseconds,
                ErrorMessage = errorMessage
            });
        }

        private string GetFullFilePath(string relativePath)
        {
            // Get the base storage path configuration (same as DocumentStorageService)
            var baseStoragePath = _configuration["DocumentStorage:BasePath"] ?? 
                Path.Combine(Directory.GetCurrentDirectory(), "uploads");
            
            return Path.Combine(baseStoragePath, relativePath);
        }

        public async Task<ProcessingResult> ConsolidateAllUserDocumentsAsync(Guid userId)
        {
            var stopwatch = Stopwatch.StartNew();
            
            try
            {
                _logger.LogInformation("Starting consolidation for UserId: {UserId}", userId);

                // Get all unlinked documents with extracted data
                var unlinkedDocuments = await _dbContext.ClinicalDocuments
                    .Where(d => d.UserId == userId && 
                               d.Status == DocumentStatus.Completed && 
                               d.ExtractedData != null &&
                               d.PatientContextId == null)
                    .ToListAsync();

                if (unlinkedDocuments.Count < 1)
                {
                    _logger.LogInformation("No unlinked documents found for user {UserId}", userId);
                    return new ProcessingResult
                    {
                        Success = false,
                        ErrorMessage = "No documents with extracted data found for consolidation"
                    };
                }

                _logger.LogInformation("Found {DocumentCount} unlinked documents for user {UserId}, proceeding with consolidation", 
                    unlinkedDocuments.Count, userId);

                // Create a new PatientContext for this consolidation
                var patientContext = new TrustFirstPlatform.Domain.Entities.PatientContext
                {
                    Id = Guid.NewGuid(),
                    PatientIdentifier = $"PAT-{DateTime.UtcNow:yyyyMMdd-HHmmss}-{Guid.NewGuid().ToString("N")[..8].ToUpper()}",
                    PatientName = $"Consolidated Patient - {DateTime.UtcNow:yyyyMMdd-HHmmss}",
                    CreatedByUserId = userId,
                    CreatedAt = DateTime.UtcNow,
                    Status = "Active"
                };
                
                _dbContext.PatientContexts.Add(patientContext);
                await _dbContext.SaveChangesAsync();
                
                _logger.LogInformation("Created new PatientContext {PatientContextId} for user {UserId}", 
                    patientContext.Id, userId);

                // Link all unlinked documents to this new PatientContext
                foreach (var doc in unlinkedDocuments)
                {
                    doc.PatientContextId = patientContext.Id;
                }

                await _dbContext.SaveChangesAsync();
                _logger.LogInformation("Linked {DocumentCount} documents to PatientContext {PatientContextId}", 
                    unlinkedDocuments.Count, patientContext.Id);

                // Call Azure OpenAI to consolidate all individual document extractions
                var consolidationResult = await _consolidationService.ConsolidatePatientDataAsync(patientContext.Id);

                if (consolidationResult.Success)
                {
                    _logger.LogInformation(
                        "Consolidation completed successfully for UserId: {UserId}, PatientContextId: {PatientContextId}. " +
                        "Documents processed: {DocumentsProcessed}, Has conflicts: {HasConflicts}",
                        userId, patientContext.Id, consolidationResult.DocumentsProcessed, consolidationResult.HasConflicts);

                    await _auditService.LogAsync(userId, "CONSOLIDATION_COMPLETED", new
                    {
                        UserId = userId,
                        PatientContextId = patientContext.Id,
                        DocumentsProcessed = consolidationResult.DocumentsProcessed,
                        HasConflicts = consolidationResult.HasConflicts
                    });

                    return new ProcessingResult
                    {
                        Success = true,
                        ProcessingDuration = stopwatch.Elapsed
                    };
                }
                else
                {
                    _logger.LogWarning(
                        "Consolidation failed for UserId: {UserId}, PatientContextId: {PatientContextId}. Error: {Error}",
                        userId, patientContext.Id, consolidationResult.ErrorMessage);

                    await _auditService.LogAsync(userId, "CONSOLIDATION_FAILED", new
                    {
                        UserId = userId,
                        PatientContextId = patientContext.Id,
                        ErrorMessage = consolidationResult.ErrorMessage,
                        DocumentsProcessed = consolidationResult.DocumentsProcessed
                    });

                    return new ProcessingResult
                    {
                        Success = false,
                        ErrorMessage = consolidationResult.ErrorMessage,
                        ProcessingDuration = stopwatch.Elapsed
                    };
                }
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                _logger.LogError(ex, "Error during consolidation for UserId: {UserId}", userId);

                await _auditService.LogAsync(userId, "CONSOLIDATION_ERROR", new
                {
                    UserId = userId,
                    ErrorMessage = ex.Message
                });

                return new ProcessingResult
                {
                    Success = false,
                    ErrorMessage = "An unexpected error occurred during consolidation",
                    ProcessingDuration = stopwatch.Elapsed
                };
            }
        }


    }
}
