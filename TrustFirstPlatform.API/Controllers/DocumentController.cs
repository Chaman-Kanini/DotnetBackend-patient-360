using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using TrustFirstPlatform.Application.DTOs;
using TrustFirstPlatform.Application.Services;

namespace TrustFirstPlatform.API.Controllers
{
    [ApiController]
    [Route("api/documents")]
    [Route("api/document")]
    [Authorize]
    [RequestSizeLimit(52_428_800)] // 50MB for single upload
    [RequestFormLimits(MultipartBodyLengthLimit = 209_715_200)] // 200MB for batch
    public class DocumentController : ControllerBase
    {
        private readonly IDocumentUploadService _documentUploadService;
        private readonly IAuditService _auditService;
        private readonly ILogger<DocumentController> _logger;

        public DocumentController(
            IDocumentUploadService documentUploadService,
            IAuditService auditService,
            ILogger<DocumentController> logger)
        {
            _documentUploadService = documentUploadService;
            _auditService = auditService;
            _logger = logger;
        }

        private Guid GetCurrentUserId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return Guid.TryParse(userIdClaim, out var userId) ? userId : Guid.Empty;
        }

        [HttpPost("upload")]
        public async Task<IActionResult> UploadDocument([FromForm] UploadDocumentRequest request)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (userId == Guid.Empty)
                {
                    return Unauthorized(new { success = false, message = "User not identified" });
                }

                var result = await _documentUploadService.UploadDocumentAsync(request.File, userId, request.PatientContextId);

                if (result.Success)
                {
                    return Ok(new
                    {
                        success = true,
                        document = new
                        {
                            id = result.Document?.Id,
                            fileName = result.Document?.OriginalFileName,
                            status = result.Document?.Status.ToString(),
                            uploadedAt = result.Document?.UploadedAt,
                            fileSize = result.Document?.FileSizeBytes
                        }
                    });
                }

                var statusCode = GetErrorStatusCode(result.ErrorType);
                return statusCode switch
                {
                    413 => StatusCode(413, new { success = false, message = result.ErrorMessage }),
                    422 => StatusCode(422, new { success = false, message = result.ErrorMessage }),
                    _ => BadRequest(new { success = false, message = result.ErrorMessage })
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading document");
                return StatusCode(500, new { success = false, message = "An unexpected error occurred" });
            }
        }

        [HttpPost("upload-batch")]
        public async Task<IActionResult> UploadDocuments([FromForm] UploadDocumentsRequest request)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (userId == Guid.Empty)
                {
                    return Unauthorized(new { success = false, message = "User not identified" });
                }

                var batchResult = await _documentUploadService.UploadDocumentsAsync(request.Files, userId, request.PatientContextId);

                var response = new
                {
                    success = batchResult.HasAnySuccess,
                    summary = new
                    {
                        totalFiles = request.Files.Count,
                        successful = batchResult.SuccessCount,
                        failed = batchResult.FailureCount
                    },
                    documents = batchResult.Results.Where(r => r.Success).Select(r => new
                    {
                        id = r.Document?.Id,
                        fileName = r.Document?.OriginalFileName,
                        status = r.Document?.Status.ToString(),
                        uploadedAt = r.Document?.UploadedAt,
                        fileSize = r.Document?.FileSizeBytes
                    }).ToList(),
                    errors = batchResult.Results.Where(r => !r.Success).Select(r => new
                    {
                        fileName = r.Document?.OriginalFileName ?? r.OriginalFileName ?? "Unknown",
                        error = r.ErrorMessage,
                        errorType = r.ErrorType?.ToString()
                    }).ToList()
                };

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading documents batch");
                return StatusCode(500, new { success = false, message = "An unexpected error occurred" });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetUserDocuments()
        {
            try
            {
                var userId = GetCurrentUserId();
                if (userId == Guid.Empty)
                {
                    return Unauthorized(new { success = false, message = "User not identified" });
                }

                var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
                await _auditService.LogAsync("DOCUMENT_LIST_VIEWED", userId, new { }, ipAddress);

                var documents = await _documentUploadService.GetUserDocumentsAsync(userId);

                var response = documents.Select(d => new
                {
                    id = d.Id,
                    fileName = d.OriginalFileName,
                    fileSize = d.FileSizeBytes,
                    contentType = d.ContentType,
                    status = d.Status.ToString(),
                    uploadedAt = d.UploadedAt,
                    processedAt = d.ProcessedAt,
                    validationError = d.ValidationError,
                    processingError = d.ProcessingError,
                    patientContextId = d.PatientContextId,
                    patientContext = d.PatientContext != null ? new
                    {
                        id = d.PatientContext.Id,
                        patientIdentifier = d.PatientContext.PatientIdentifier,
                        patientName = d.PatientContext.PatientName
                    } : null
                });

                return Ok(new { success = true, documents = response });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving user documents");
                return StatusCode(500, new { success = false, message = "An unexpected error occurred" });
            }
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetDocument(Guid id)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (userId == Guid.Empty)
                {
                    return Unauthorized(new { success = false, message = "User not identified" });
                }

                var document = await _documentUploadService.GetDocumentAsync(id, userId);
                if (document == null)
                {
                    return NotFound(new { success = false, message = "Document not found" });
                }

                var response = new
                {
                    id = document.Id,
                    fileName = document.OriginalFileName,
                    fileSize = document.FileSizeBytes,
                    contentType = document.ContentType,
                    status = document.Status.ToString(),
                    uploadedAt = document.UploadedAt,
                    processedAt = document.ProcessedAt,
                    validationError = document.ValidationError,
                    processingError = document.ProcessingError,
                    patientContextId = document.PatientContextId,
                    patientContext = document.PatientContext != null ? new
                    {
                        id = document.PatientContext.Id,
                        patientIdentifier = document.PatientContext.PatientIdentifier,
                        patientName = document.PatientContext.PatientName
                    } : null
                };

                return Ok(new { success = true, document = response });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving document {DocumentId}", id);
                return StatusCode(500, new { success = false, message = "An unexpected error occurred" });
            }
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteDocument(Guid id)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (userId == Guid.Empty)
                {
                    return Unauthorized(new { success = false, message = "User not identified" });
                }

                var result = await _documentUploadService.DeleteDocumentAsync(id, userId);
                if (result.NotFound)
                {
                    return NotFound(new { success = false, message = "Document not found" });
                }

                if (!result.Success)
                {
                    return StatusCode(500, new { success = false, message = result.ErrorMessage ?? "Failed to delete document" });
                }

                return Ok(new { success = true, message = "Document deleted successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting document {DocumentId}", id);
                return StatusCode(500, new { success = false, message = "An unexpected error occurred" });
            }
        }

        [HttpGet("{id}/download")]
        public async Task<IActionResult> DownloadDocument(Guid id)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (userId == Guid.Empty)
                {
                    return Unauthorized(new { success = false, message = "User not identified" });
                }

                var fileResult = await _documentUploadService.GetDocumentFileAsync(id, userId);
                if (fileResult == null)
                {
                    return NotFound(new { success = false, message = "Document not found" });
                }

                return File(fileResult.Stream, fileResult.ContentType, fileResult.FileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error downloading document {DocumentId}", id);
                return StatusCode(500, new { success = false, message = "An unexpected error occurred" });
            }
        }

        private static int GetErrorStatusCode(ValidationErrorType? errorType)
        {
            return errorType switch
            {
                ValidationErrorType.FileTooLarge => 413, // Payload Too Large
                ValidationErrorType.PasswordProtected => 422, // Unprocessable Entity
                ValidationErrorType.CorruptedFile => 422, // Unprocessable Entity
                _ => 400 // Bad Request
            };
        }
    }
}
