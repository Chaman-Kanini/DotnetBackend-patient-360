using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using TrustFirstPlatform.Domain.Entities;
using TrustFirstPlatform.Infrastructure.Data;

namespace TrustFirstPlatform.Application.Services
{
    public class DocumentUploadService : IDocumentUploadService
    {
        private readonly AppDbContext _dbContext;
        private readonly IFileValidationService _fileValidationService;
        private readonly IDocumentStorageService _documentStorageService;
        private readonly IAuditService _auditService;
        private readonly IServiceScopeFactory _serviceScopeFactory;
        private readonly ILogger<DocumentUploadService> _logger;

        public DocumentUploadService(
            AppDbContext dbContext,
            IFileValidationService fileValidationService,
            IDocumentStorageService documentStorageService,
            IAuditService auditService,
            IServiceScopeFactory serviceScopeFactory,
            ILogger<DocumentUploadService> logger)
        {
            _dbContext = dbContext;
            _fileValidationService = fileValidationService;
            _documentStorageService = documentStorageService;
            _auditService = auditService;
            _serviceScopeFactory = serviceScopeFactory;
            _logger = logger;
        }

        public async Task<UploadResult> UploadDocumentAsync(IFormFile file, Guid userId, Guid? patientContextId, bool skipProcessing = false)
        {
            try
            {
                // Validate file
                var validationResult = await _fileValidationService.ValidateFileAsync(file);
                if (!validationResult.IsValid)
                {
                    await _auditService.LogAsync(userId, "DOCUMENT_VALIDATION_REJECTED", new
                    {
                        FileName = file.FileName,
                        FileSize = file.Length,
                        ErrorMessage = validationResult.ErrorMessage,
                        ErrorType = validationResult.ErrorType
                    });

                    return new UploadResult
                    {
                        Success = false,
                        OriginalFileName = file.FileName,
                        ErrorMessage = validationResult.ErrorMessage,
                        ErrorType = validationResult.ErrorType
                    };
                }

                // Store file
                var storageResult = await _documentStorageService.SaveFileAsync(file, userId);
                if (!storageResult.Success)
                {
                    await _auditService.LogAsync(userId, "DOCUMENT_STORAGE_FAILED", new
                    {
                        FileName = file.FileName,
                        ErrorMessage = storageResult.ErrorMessage
                    });

                    return new UploadResult
                    {
                        Success = false,
                        OriginalFileName = file.FileName,
                        ErrorMessage = storageResult.ErrorMessage
                    };
                }

                // Create database record
                var document = new ClinicalDocument
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    PatientContextId = patientContextId,
                    OriginalFileName = file.FileName,
                    StoredFileName = storageResult.StoredFileName!,
                    ContentType = file.ContentType,
                    FileSizeBytes = file.Length,
                    FileExtension = Path.GetExtension(file.FileName),
                    StoragePath = storageResult.StoragePath!,
                    FileHash = storageResult.FileHash!,
                    Status = DocumentStatus.Validated,
                    UploadedAt = DateTime.UtcNow,
                    Metadata = CreateMetadata(file)
                };

                _dbContext.ClinicalDocuments.Add(document);
                await _dbContext.SaveChangesAsync();

                await _auditService.LogAsync(userId, "DOCUMENT_UPLOAD_SUCCESS", new
                {
                    DocumentId = document.Id,
                    FileName = file.FileName,
                    FileSize = file.Length,
                    StoragePath = storageResult.StoragePath
                });

                // Start document processing in background ONLY if not part of a batch
                if (!skipProcessing)
                {
                    _ = Task.Run(async () => {
                        try
                        {
                            using var scope = _serviceScopeFactory.CreateScope();
                            var processingService = scope.ServiceProvider.GetRequiredService<IDocumentProcessingService>();
                            await processingService.ProcessDocumentAsync(document.Id, userId);
                            _logger.LogInformation("Background processing completed for document {DocumentId}", document.Id);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Background processing failed for document {DocumentId}", document.Id);
                        }
                    });
                }

                return new UploadResult
                {
                    Success = true,
                    Document = document,
                    OriginalFileName = file.FileName
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading document {FileName} for user {UserId}", file.FileName, userId);
                
                await _auditService.LogAsync(userId, "DOCUMENT_UPLOAD_FAILED", new
                {
                    FileName = file.FileName,
                    ErrorMessage = ex.Message
                });

                return new UploadResult
                {
                    Success = false,
                    OriginalFileName = file.FileName,
                    ErrorMessage = "An unexpected error occurred while uploading the document"
                };
            }
        }

        public async Task<BatchUploadResult> UploadDocumentsAsync(IFormFileCollection files, Guid userId, Guid? patientContextId)
        {
            // Create batch session
            var batch = new UploadBatch
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                TotalDocuments = files.Count,
                ProcessedDocuments = 0,
                Status = "Processing",
                CreatedAt = DateTime.UtcNow
            };
            _dbContext.UploadBatches.Add(batch);
            await _dbContext.SaveChangesAsync();

            var batchResult = new BatchUploadResult();
            var documentIds = new List<Guid>();

            // Upload all files (fast, no processing)
            foreach (var file in files)
            {
                var result = await UploadDocumentAsync(file, userId, patientContextId, skipProcessing: true);
                batchResult.Results.Add(result);
                
                if (result.Success && result.Document != null)
                {
                    // Link to batch
                    result.Document.UploadBatchId = batch.Id;
                    documentIds.Add(result.Document.Id);
                }
            }
            await _dbContext.SaveChangesAsync();

            // Start SINGLE background task for entire batch
            if (documentIds.Any())
            {
                _ = Task.Run(async () => {
                    try
                    {
                        using var scope = _serviceScopeFactory.CreateScope();
                        var batchProcessor = scope.ServiceProvider.GetRequiredService<IBatchProcessingService>();
                        await batchProcessor.ProcessBatchAsync(batch.Id, userId, documentIds);
                        _logger.LogInformation("Batch processing completed for batch {BatchId}", batch.Id);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Batch processing failed for batch {BatchId}", batch.Id);
                    }
                });
            }

            return batchResult;
        }

        public async Task<ClinicalDocument?> GetDocumentAsync(Guid documentId, Guid userId)
        {
            try
            {
                var document = await _dbContext.ClinicalDocuments
                    .Include(d => d.User)
                    .Include(d => d.PatientContext)
                    .FirstOrDefaultAsync(d => d.Id == documentId && d.UserId == userId);

                return document;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving document {DocumentId} for user {UserId}", documentId, userId);
                return null;
            }
        }

        public async Task<IEnumerable<ClinicalDocument>> GetUserDocumentsAsync(Guid userId)
        {
            try
            {
                return await _dbContext.ClinicalDocuments
                    .Include(d => d.PatientContext)
                    .Where(d => d.UserId == userId)
                    .OrderByDescending(d => d.UploadedAt)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving documents for user {UserId}", userId);
                return Enumerable.Empty<ClinicalDocument>();
            }
        }

        public async Task<DocumentFileResult?> GetDocumentFileAsync(Guid documentId, Guid userId)
        {
            try
            {
                var document = await _dbContext.ClinicalDocuments
                    .FirstOrDefaultAsync(d => d.Id == documentId && d.UserId == userId);

                if (document == null)
                {
                    return null;
                }

                var stream = await _documentStorageService.GetFileAsync(document.StoragePath);

                await _auditService.LogAsync(userId, "DOCUMENT_DOWNLOADED", new
                {
                    DocumentId = document.Id,
                    FileName = document.OriginalFileName,
                    FileSizeBytes = document.FileSizeBytes
                });

                return new DocumentFileResult
                {
                    Stream = stream,
                    FileName = document.OriginalFileName,
                    ContentType = document.ContentType
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting file for document {DocumentId} for user {UserId}", documentId, userId);

                await _auditService.LogAsync(userId, "DOCUMENT_DOWNLOAD_FAILED", new
                {
                    DocumentId = documentId,
                    ErrorMessage = ex.Message
                });

                return null;
            }
        }

        public async Task<DeleteDocumentResult> DeleteDocumentAsync(Guid documentId, Guid userId)
        {
            try
            {
                var document = await _dbContext.ClinicalDocuments
                    .FirstOrDefaultAsync(d => d.Id == documentId && d.UserId == userId);

                if (document == null)
                {
                    return new DeleteDocumentResult
                    {
                        Success = false,
                        NotFound = true
                    };
                }

                var fileDeleted = await _documentStorageService.DeleteFileAsync(document.StoragePath);

                _dbContext.ClinicalDocuments.Remove(document);
                await _dbContext.SaveChangesAsync();

                await _auditService.LogAsync(userId, "DOCUMENT_DELETED", new
                {
                    DocumentId = document.Id,
                    FileName = document.OriginalFileName,
                    StoragePath = document.StoragePath,
                    FileDeleted = fileDeleted
                });

                return new DeleteDocumentResult
                {
                    Success = true,
                    NotFound = false
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting document {DocumentId} for user {UserId}", documentId, userId);

                await _auditService.LogAsync(userId, "DOCUMENT_DELETE_FAILED", new
                {
                    DocumentId = documentId,
                    ErrorMessage = ex.Message
                });

                return new DeleteDocumentResult
                {
                    Success = false,
                    NotFound = false,
                    ErrorMessage = "An unexpected error occurred while deleting the document"
                };
            }
        }

        private JsonDocument CreateMetadata(IFormFile file)
        {
            var metadata = new
            {
                OriginalFileName = file.FileName,
                ContentType = file.ContentType,
                ContentDisposition = file.ContentDisposition,
                Headers = file.Headers.ToDictionary(h => h.Key, h => h.Value.ToString()),
                UploadedAt = DateTime.UtcNow,
                ClientFileInfo = new
                {
                    Name = file.Name,
                    Length = file.Length
                }
            };

            var json = JsonSerializer.Serialize(metadata);
            return JsonDocument.Parse(json);
        }
    }
}
