using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using TrustFirstPlatform.Application.Models;
using TrustFirstPlatform.Application.Services;
using TrustFirstPlatform.Domain.Entities;
using TrustFirstPlatform.Infrastructure.Data;

namespace TrustFirstPlatform.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class PatientController : ControllerBase
    {
        private readonly IConsolidationService _consolidationService;
        private readonly IConflictService _conflictService;
        private readonly IDocumentProcessingService _documentProcessingService;
        private readonly IMedicalCodeLookupService _medicalCodeLookupService;
        private readonly IPatientChatbotService _chatbotService;
        private readonly IAuditService _auditService;
        private readonly AppDbContext _dbContext;
        private readonly ILogger<PatientController> _logger;

        public PatientController(
            IConsolidationService consolidationService,
            IConflictService conflictService,
            IDocumentProcessingService documentProcessingService,
            IMedicalCodeLookupService medicalCodeLookupService,
            IPatientChatbotService chatbotService,
            IAuditService auditService,
            AppDbContext dbContext,
            ILogger<PatientController> logger)
        {
            _consolidationService = consolidationService;
            _conflictService = conflictService;
            _documentProcessingService = documentProcessingService;
            _medicalCodeLookupService = medicalCodeLookupService;
            _chatbotService = chatbotService;
            _auditService = auditService;
            _dbContext = dbContext;
            _logger = logger;
        }

        [HttpPost("{id}/consolidate")]
        public async Task<IActionResult> ConsolidatePatientData(Guid id, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Consolidation request received for PatientContextId: {PatientContextId}", id);

            var patientContext = await _dbContext.PatientContexts
                .FirstOrDefaultAsync(pc => pc.Id == id, cancellationToken);

            if (patientContext == null)
            {
                _logger.LogWarning("Patient context not found: {PatientContextId}", id);
                return NotFound(new { message = "Patient context not found" });
            }

            var result = await _consolidationService.ConsolidatePatientDataAsync(id, cancellationToken);

            if (!result.Success)
            {
                _logger.LogError(
                    "Consolidation failed for PatientContextId: {PatientContextId}. Error: {ErrorMessage}",
                    id, result.ErrorMessage);

                return BadRequest(new
                {
                    message = "Consolidation failed",
                    error = result.ErrorMessage,
                    retryCount = result.RetryCount,
                    documentsProcessed = result.DocumentsProcessed
                });
            }

            _logger.LogInformation(
                "Consolidation completed successfully for PatientContextId: {PatientContextId}. Status: {Status}, HasConflicts: {HasConflicts}",
                id, patientContext.Status, result.HasConflicts);

            return Ok(new
            {
                message = "Patient data consolidated successfully",
                patientContextId = id,
                status = patientContext.Status,
                hasConflicts = result.HasConflicts,
                documentsProcessed = result.DocumentsProcessed,
                lastConsolidatedAt = patientContext.LastConsolidatedAt,
                consolidatedData = result.ConsolidatedData
            });
        }

        [HttpGet("{id}/consolidated-data")]
        public async Task<IActionResult> GetConsolidatedData(Guid id, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Retrieving consolidated data for PatientContextId: {PatientContextId}", id);

            var patientContext = await _dbContext.PatientContexts
                .FirstOrDefaultAsync(pc => pc.Id == id, cancellationToken);

            if (patientContext == null)
            {
                _logger.LogWarning("Patient context not found: {PatientContextId}", id);
                return NotFound(new { message = "Patient context not found" });
            }

            if (patientContext.ConsolidatedData == null)
            {
                _logger.LogInformation("No consolidated data available for PatientContextId: {PatientContextId}", id);
                return NotFound(new { message = "No consolidated data available. Please run consolidation first." });
            }

            var userId = GetCurrentUserId();
            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            await _auditService.LogAsync("VIEW_PATIENT", userId, new
            {
                PatientContextId = id,
                PatientIdentifier = patientContext.PatientIdentifier,
                PatientName = patientContext.PatientName
            }, ipAddress);

            // Enrich with medical codes
            var medicalCodes = await _medicalCodeLookupService.EnrichConsolidatedDataWithCodesAsync(patientContext.ConsolidatedData);

            // Extract patient summary from consolidated data
            string? patientSummary = null;
            if (patientContext.ConsolidatedData?.RootElement.TryGetProperty("patient_summary", out var summaryElement) == true)
            {
                patientSummary = summaryElement.GetString();
            }

            return Ok(new
            {
                patientContextId = id,
                patientIdentifier = patientContext.PatientIdentifier,
                patientName = patientContext.PatientName,
                status = patientContext.Status,
                lastConsolidatedAt = patientContext.LastConsolidatedAt,
                consolidatedData = patientContext.ConsolidatedData,
                patientSummary = patientSummary,
                medicalCodes = new
                {
                    icd10Codes = medicalCodes.ICD10Codes,
                    cptCodes = medicalCodes.CPTCodes,
                    summary = new
                    {
                        totalDiagnoses = medicalCodes.TotalDiagnoses,
                        diagnosesMatched = medicalCodes.DiagnosesMatched,
                        totalProcedures = medicalCodes.TotalProcedures,
                        proceduresMatched = medicalCodes.ProceduresMatched
                    }
                }
            });
        }

        [HttpGet("{id}/conflicts")]
        public async Task<IActionResult> GetConflicts(
            Guid id,
            [FromQuery] string? category = null,
            [FromQuery] string? severity = null,
            CancellationToken cancellationToken = default)
        {
            _logger.LogInformation(
                "Retrieving conflicts for PatientContextId: {PatientContextId}, Category: {Category}, Severity: {Severity}",
                id, category ?? "All", severity ?? "All");

            var patientContext = await _dbContext.PatientContexts
                .FirstOrDefaultAsync(pc => pc.Id == id, cancellationToken);

            if (patientContext == null)
            {
                _logger.LogWarning("Patient context not found: {PatientContextId}", id);
                return NotFound(new { message = "Patient context not found" });
            }

            if (patientContext.ConsolidatedData == null)
            {
                _logger.LogInformation(
                    "No consolidated data available for PatientContextId: {PatientContextId}",
                    id);
                return NotFound(new { message = "No consolidated data available. Please run consolidation first." });
            }

            var conflictSection = _conflictService.ParseConflicts(patientContext.ConsolidatedData);
            
            if (conflictSection == null || !patientContext.HasConflicts)
            {
                _logger.LogInformation(
                    "No conflicts found for PatientContextId: {PatientContextId}",
                    id);
                return Ok(new
                {
                    patientContextId = id,
                    hasConflicts = false,
                    conflictCount = 0,
                    conflicts = new { },
                    summary = new
                    {
                        totalConflicts = 0,
                        criticalConflicts = 0,
                        warningConflicts = 0,
                        infoConflicts = 0,
                        conflictsByCategory = new { }
                    }
                });
            }

            var summary = _conflictService.GetConflictSummary(conflictSection);
            object conflicts;

            if (!string.IsNullOrWhiteSpace(category))
            {
                var categoryConflicts = _conflictService.GetConflictsByCategory(conflictSection, category);
                conflicts = new Dictionary<string, object> { { category, categoryConflicts } };
                _logger.LogInformation(
                    "Retrieved {Count} conflicts for category {Category}",
                    categoryConflicts.Count, category);
            }
            else if (!string.IsNullOrWhiteSpace(severity))
            {
                if (Enum.TryParse<ConflictSeverity>(severity, true, out var severityEnum))
                {
                    var severityConflicts = _conflictService.GetConflictsBySeverity(conflictSection, severityEnum);
                    conflicts = new Dictionary<string, object> { { severity, severityConflicts } };
                    _logger.LogInformation(
                        "Retrieved {Count} conflicts with severity {Severity}",
                        severityConflicts.Count, severity);
                }
                else
                {
                    return BadRequest(new { message = $"Invalid severity value: {severity}. Valid values are: Info, Warning, Critical" });
                }
            }
            else
            {
                conflicts = conflictSection;
            }

            return Ok(new
            {
                patientContextId = id,
                patientIdentifier = patientContext.PatientIdentifier,
                patientName = patientContext.PatientName,
                hasConflicts = patientContext.HasConflicts,
                conflictCount = patientContext.ConflictCount,
                lastConsolidatedAt = patientContext.LastConsolidatedAt,
                conflicts,
                summary
            });
        }

        private Guid GetCurrentUserId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return Guid.TryParse(userIdClaim, out var userId) ? userId : Guid.Empty;
        }

        [HttpPost("consolidate-all")]
        public async Task<IActionResult> ConsolidateAllDocuments(CancellationToken cancellationToken)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (userId == Guid.Empty)
                {
                    return Unauthorized(new { success = false, message = "User not identified" });
                }

                _logger.LogInformation("Consolidation request for all user documents. UserId: {UserId}", userId);

                var result = await _documentProcessingService.ConsolidateAllUserDocumentsAsync(userId);

                if (result.Success)
                {
                    return Ok(new
                    {
                        success = true,
                        message = "All documents consolidated successfully"
                    });
                }
                else
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = result.ErrorMessage
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during consolidation of all documents");
                return StatusCode(500, new { success = false, message = "An unexpected error occurred" });
            }
        }

        [HttpPost("consolidate-recent")]
        public async Task<IActionResult> ConsolidateRecentUploads([FromBody] ConsolidateRecentRequest request, CancellationToken cancellationToken)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (userId == Guid.Empty)
                {
                    return Unauthorized(new { success = false, message = "User not identified" });
                }

                _logger.LogInformation("Consolidation request for recent uploads: {DocumentCount} documents", request.DocumentIds.Count);

                // Verify all documents belong to the current user and are completed
                var documents = await _dbContext.ClinicalDocuments
                    .Where(d => request.DocumentIds.Contains(d.Id) && 
                               d.UserId == userId && 
                               d.Status == DocumentStatus.Completed &&
                               d.ExtractedData != null)
                    .ToListAsync(cancellationToken);

                if (documents.Count != request.DocumentIds.Count)
                {
                    _logger.LogWarning("Document count mismatch. Expected: {Expected}, Found: {Found}", 
                        request.DocumentIds.Count, documents.Count);
                    return BadRequest(new { 
                        success = false, 
                        message = "Some documents are not valid or not ready for consolidation" 
                    });
                }

                // Create new PatientContext for this consolidation session
                var patientContext = new PatientContext
                {
                    Id = Guid.NewGuid(),
                    PatientIdentifier = $"PAT-{DateTime.UtcNow:yyyyMMdd-HHmmss}-{Guid.NewGuid().ToString("N")[..8].ToUpper()}",
                    PatientName = $"Consolidated Patient - {DateTime.UtcNow:yyyyMMdd-HHmmss}",
                    CreatedByUserId = userId,
                    CreatedAt = DateTime.UtcNow,
                    Status = "Active"
                };

                _dbContext.PatientContexts.Add(patientContext);

                // Link documents to the new PatientContext
                foreach (var doc in documents)
                {
                    doc.PatientContextId = patientContext.Id;
                }

                await _dbContext.SaveChangesAsync(cancellationToken);

                // Trigger consolidation
                var consolidationResult = await _consolidationService.ConsolidatePatientDataAsync(patientContext.Id, cancellationToken);

                if (consolidationResult.Success)
                {
                    _logger.LogInformation(
                        "Recent uploads consolidation completed successfully. PatientContextId: {PatientContextId}, Documents: {DocumentCount}",
                        patientContext.Id, consolidationResult.DocumentsProcessed);

                    return Ok(new
                    {
                        success = true,
                        patientContextId = patientContext.Id,
                        message = "Documents consolidated successfully",
                        documentsProcessed = consolidationResult.DocumentsProcessed,
                        hasConflicts = consolidationResult.HasConflicts
                    });
                }
                else
                {
                    _logger.LogError(
                        "Recent uploads consolidation failed for PatientContextId: {PatientContextId}. Error: {Error}",
                        patientContext.Id, consolidationResult.ErrorMessage);

                    return BadRequest(new
                    {
                        success = false,
                        message = "Consolidation failed",
                        error = consolidationResult.ErrorMessage
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during recent uploads consolidation");
                return StatusCode(500, new { success = false, message = "An unexpected error occurred" });
            }
        }

        [HttpGet("{id}/medical-codes")]
        public async Task<IActionResult> GetMedicalCodes(Guid id, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Medical codes request for PatientContextId: {PatientContextId}", id);

            var patientContext = await _dbContext.PatientContexts
                .FirstOrDefaultAsync(pc => pc.Id == id, cancellationToken);

            if (patientContext == null)
            {
                _logger.LogWarning("Patient context not found: {PatientContextId}", id);
                return NotFound(new { message = "Patient context not found" });
            }

            if (patientContext.ConsolidatedData == null)
            {
                _logger.LogInformation("No consolidated data available for PatientContextId: {PatientContextId}", id);
                return NotFound(new { message = "No consolidated data available. Please run consolidation first." });
            }

            try
            {
                var medicalCodes = await _medicalCodeLookupService.EnrichConsolidatedDataWithCodesAsync(patientContext.ConsolidatedData);

                _logger.LogInformation(
                    "Medical codes retrieved for PatientContextId: {PatientContextId}. ICD-10: {ICD10Count}, CPT: {CPTCount}",
                    id, medicalCodes.ICD10Codes.Count, medicalCodes.CPTCodes.Count);

                return Ok(new
                {
                    patientContextId = id,
                    patientIdentifier = patientContext.PatientIdentifier,
                    patientName = patientContext.PatientName,
                    medicalCodes = new
                    {
                        icd10Codes = medicalCodes.ICD10Codes,
                        cptCodes = medicalCodes.CPTCodes,
                        summary = new
                        {
                            totalDiagnoses = medicalCodes.TotalDiagnoses,
                            diagnosesMatched = medicalCodes.DiagnosesMatched,
                            totalProcedures = medicalCodes.TotalProcedures,
                            proceduresMatched = medicalCodes.ProceduresMatched
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving medical codes for PatientContextId: {PatientContextId}", id);
                return StatusCode(500, new { message = "An error occurred while retrieving medical codes" });
            }
        }

        [HttpPost("{id}/chatbot")]
        public async Task<IActionResult> AskChatbot(Guid id, [FromBody] ChatbotRequest request, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(request.Question))
            {
                return BadRequest(new { message = "Question cannot be empty" });
            }

            _logger.LogInformation(
                "Chatbot question received for PatientContextId: {PatientContextId}",
                id);

            try
            {
                var response = await _chatbotService.AskQuestionAsync(id, request.Question, cancellationToken);

                if (!response.Success)
                {
                    _logger.LogWarning(
                        "Chatbot request failed for PatientContextId: {PatientContextId}. Error: {Error}",
                        id, response.ErrorMessage);

                    return BadRequest(new
                    {
                        success = false,
                        message = response.Answer,
                        error = response.ErrorMessage
                    });
                }

                _logger.LogInformation(
                    "Chatbot response generated successfully for PatientContextId: {PatientContextId}",
                    id);

                return Ok(new
                {
                    success = true,
                    question = response.Question,
                    answer = response.Answer
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing chatbot request for PatientContextId: {PatientContextId}", id);
                return StatusCode(500, new { message = "An error occurred while processing your question" });
            }
        }

        [HttpPost("chatbot/ask")]
        public async Task<IActionResult> AskChatbotWithHistory([FromBody] ChatbotHistoryRequest request, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(request.Question))
            {
                return BadRequest(new { message = "Question cannot be empty" });
            }

            var userId = GetCurrentUserId();
            if (userId == Guid.Empty)
            {
                return Unauthorized(new { success = false, message = "User not identified" });
            }

            _logger.LogInformation(
                "Chatbot question with history received for UserId: {UserId}, BatchId: {BatchId}",
                userId, request.BatchId ?? "None");

            try
            {
                var response = await _chatbotService.AskQuestionWithHistoryAsync(
                    userId, 
                    request.BatchId, 
                    request.Question, 
                    cancellationToken);

                if (!response.Success)
                {
                    _logger.LogWarning(
                        "Chatbot request failed for UserId: {UserId}. Error: {Error}",
                        userId, response.ErrorMessage);

                    return BadRequest(new
                    {
                        success = false,
                        message = response.Answer,
                        error = response.ErrorMessage
                    });
                }

                _logger.LogInformation(
                    "Chatbot response generated and saved successfully for UserId: {UserId}",
                    userId);

                return Ok(new
                {
                    success = true,
                    question = response.Question,
                    answer = response.Answer
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing chatbot request for UserId: {UserId}", userId);
                return StatusCode(500, new { message = "An error occurred while processing your question" });
            }
        }

        [HttpGet("chatbot/history")]
        public async Task<IActionResult> GetChatHistory([FromQuery] string? batchId = null, CancellationToken cancellationToken = default)
        {
            var userId = GetCurrentUserId();
            if (userId == Guid.Empty)
            {
                return Unauthorized(new { success = false, message = "User not identified" });
            }

            _logger.LogInformation(
                "Chat history request for UserId: {UserId}, BatchId: {BatchId}",
                userId, batchId ?? "All");

            try
            {
                var history = await _chatbotService.GetChatHistoryAsync(userId, batchId, cancellationToken);

                _logger.LogInformation(
                    "Retrieved {Count} chat history records for UserId: {UserId}",
                    history.Count, userId);

                return Ok(new
                {
                    success = true,
                    history = history.Select(h => new
                    {
                        id = h.Id,
                        batchId = h.BatchId,
                        question = h.Question,
                        answer = h.Answer,
                        timestamp = h.Timestamp
                    }),
                    totalCount = history.Count
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving chat history for UserId: {UserId}", userId);
                return StatusCode(500, new { message = "An error occurred while retrieving chat history" });
            }
        }

        [HttpGet("rag-batches")]
        public async Task<IActionResult> GetRagBatches()
        {
            try
            {
                var ragDataBasePath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "demo_rag", "backend", "RagApi", "app", "rag_data"));
                var ragOutputPath = Path.Combine(ragDataBasePath, "output");
                
                if (!Directory.Exists(ragOutputPath))
                {
                    return NotFound(new { message = "RAG output directory not found" });
                }

                var batches = new List<object>();
                var batchDirectories = Directory.GetDirectories(ragOutputPath, "batch_*");

                foreach (var batchDir in batchDirectories)
                {
                    var batchName = Path.GetFileName(batchDir);
                    var jsonFile = Path.Combine(batchDir, "clinical_consolidated_output.json");
                    
                    if (System.IO.File.Exists(jsonFile))
                    {
                        var fileInfo = new FileInfo(jsonFile);
                        batches.Add(new
                        {
                            id = batchName,
                            name = batchName,
                            createdAt = fileInfo.CreationTime,
                            lastModified = fileInfo.LastWriteTime,
                            size = fileInfo.Length,
                            filePath = jsonFile
                        });
                    }
                }

                // Sort by creation time (newest first)
                batches = batches.OrderByDescending(b => ((dynamic)b).createdAt).ToList();

                return Ok(new
                {
                    success = true,
                    batches = batches,
                    totalCount = batches.Count
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving RAG batches");
                return StatusCode(500, new { message = "An error occurred while retrieving RAG batches" });
            }
        }

        [HttpGet("rag-batches/{batchId}")]
        public async Task<IActionResult> GetRagBatchData(string batchId)
        {
            try
            {
                var ragDataBasePath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "demo_rag", "backend", "RagApi", "app", "rag_data"));
                var ragOutputPath = Path.Combine(ragDataBasePath, "output");
                var batchDir = Path.Combine(ragOutputPath, batchId);
                var jsonFile = Path.Combine(batchDir, "clinical_consolidated_output.json");

                if (!System.IO.File.Exists(jsonFile))
                {
                    return NotFound(new { message = "Batch data not found" });
                }

                var jsonContent = await System.IO.File.ReadAllTextAsync(jsonFile);
                
                return Ok(new
                {
                    success = true,
                    batchId = batchId,
                    data = jsonContent
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving RAG batch data for batch: {BatchId}", batchId);
                return StatusCode(500, new { message = "An error occurred while retrieving batch data" });
            }
        }

        [HttpGet("rag-batches/{batchId}/pdfs")]
        public IActionResult GetRagBatchPdfs(string batchId)
        {
            try
            {
                _logger.LogInformation("PDF request received for batch: {BatchId}", batchId);
                var ragDataBasePath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "demo_rag", "backend", "RagApi", "app", "rag_data"));
                var pdfPath = Path.Combine(ragDataBasePath, "pdfs");
                // Handle batchId that may or may not already have 'batch_' prefix
                var batchDirName = batchId.StartsWith("batch_") ? batchId : $"batch_{batchId}";
                var batchPdfDir = Path.Combine(pdfPath, batchDirName);
                _logger.LogInformation("Looking for PDFs in directory: {Directory}", batchPdfDir);

                if (!Directory.Exists(batchPdfDir))
                {
                    _logger.LogWarning("PDF directory not found: {Directory}", batchPdfDir);
                    return Ok(new
                    {
                        success = true,
                        batchId = batchId,
                        pdfs = new List<object>()
                    });
                }

                var supportedExtensions = new[] { "*.pdf", "*.doc", "*.docx" };
                var allFiles = supportedExtensions
                    .SelectMany(ext => Directory.GetFiles(batchPdfDir, ext))
                    .ToArray();
                _logger.LogInformation("Found {Count} document files", allFiles.Length);
                var pdfs = new List<object>();

                foreach (var docFile in allFiles)
                {
                    var fileInfo = new FileInfo(docFile);
                    pdfs.Add(new
                    {
                        name = fileInfo.Name,
                        fileName = fileInfo.Name,
                        size = fileInfo.Length,
                        uploadedAt = fileInfo.CreationTime,
                        lastModified = fileInfo.LastWriteTime,
                        filePath = docFile,
                        status = "Processed"
                    });
                }

                return Ok(new
                {
                    success = true,
                    batchId = batchId,
                    pdfs = pdfs,
                    totalCount = pdfs.Count
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving PDF files for batch: {BatchId}", batchId);
                return StatusCode(500, new { message = "An error occurred while retrieving PDF files" });
            }
        }

        [HttpGet("rag-batches/{batchId}/pdfs/{fileName}")]
        public IActionResult GetPdfFile(string batchId, string fileName, [FromQuery] bool download = false)
        {
            try
            {
                _logger.LogInformation("PDF file request: Batch={BatchId}, File={FileName}, Download={Download}", 
                    batchId, fileName, download);
                
                var ragDataBasePath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "demo_rag", "backend", "RagApi", "app", "rag_data"));
                var pdfPath = Path.Combine(ragDataBasePath, "pdfs");
                // Handle batchId that may or may not already have 'batch_' prefix
                var batchDirName = batchId.StartsWith("batch_") ? batchId : $"batch_{batchId}";
                var batchPdfDir = Path.Combine(pdfPath, batchDirName);
                var filePath = Path.Combine(batchPdfDir, fileName);

                if (!System.IO.File.Exists(filePath))
                {
                    _logger.LogWarning("Document file not found: {FilePath}", filePath);
                    return NotFound(new { message = "Document file not found" });
                }

                var fileBytes = System.IO.File.ReadAllBytes(filePath);
                var extension = Path.GetExtension(fileName).ToLowerInvariant();
                var contentType = extension switch
                {
                    ".pdf" => "application/pdf",
                    ".doc" => "application/msword",
                    ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                    _ => "application/octet-stream"
                };

                if (download)
                {
                    return File(fileBytes, contentType, fileName);
                }
                else
                {
                    return File(fileBytes, contentType);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving PDF file: Batch={BatchId}, File={FileName}", batchId, fileName);
                return StatusCode(500, new { message = "An error occurred while retrieving the PDF file" });
            }
        }
    }

    public class ConsolidateRecentRequest
    {
        public List<Guid> DocumentIds { get; set; } = new();
    }

    public class ChatbotRequest
    {
        public string Question { get; set; } = string.Empty;
    }

    public class ChatbotHistoryRequest
    {
        public string Question { get; set; } = string.Empty;
        public string? BatchId { get; set; }
    }
}
