using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Linq;
using TrustFirstPlatform.Application.Constants;
using TrustFirstPlatform.Application.Models;
using TrustFirstPlatform.Infrastructure.Data;

namespace TrustFirstPlatform.Application.Services
{
    public class ConsolidationService : IConsolidationService
    {
        private readonly AppDbContext _dbContext;
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        private readonly ILogger<ConsolidationService> _logger;
        private readonly IConflictService _conflictService;
        private readonly IMedicalCodeLookupService _medicalCodeLookupService;
        private const int MaxRetries = 3;
        private static readonly int[] RetryDelaysSeconds = { 10, 20, 30 };

        public ConsolidationService(
            AppDbContext dbContext,
            HttpClient httpClient,
            IConfiguration configuration,
            ILogger<ConsolidationService> logger,
            IConflictService conflictService,
            IMedicalCodeLookupService medicalCodeLookupService)
        {
            _dbContext = dbContext;
            _httpClient = httpClient;
            _configuration = configuration;
            _logger = logger;
            _conflictService = conflictService;
            _medicalCodeLookupService = medicalCodeLookupService;
        }

        public async Task<ConsolidationResult> ConsolidatePatientDataAsync(
            Guid patientContextId,
            CancellationToken cancellationToken = default)
        {
            _logger.LogInformation(
                "Starting patient data consolidation for PatientContextId: {PatientContextId}",
                patientContextId);

            var patientContext = await _dbContext.PatientContexts
                .FirstOrDefaultAsync(pc => pc.Id == patientContextId, cancellationToken);

            if (patientContext == null)
            {
                _logger.LogWarning(
                    "Patient context not found: {PatientContextId}",
                    patientContextId);
                return new ConsolidationResult
                {
                    Success = false,
                    ErrorMessage = "Patient context not found",
                    RetryCount = 0
                };
            }

            var documents = await _dbContext.ClinicalDocuments
                .Where(cd => cd.PatientContextId == patientContextId && cd.ExtractedData != null)
                .Select(cd => cd.ExtractedData)
                .ToListAsync(cancellationToken);

            if (documents.Count == 0)
            {
                _logger.LogWarning(
                    "No documents with extracted data found for PatientContextId: {PatientContextId}",
                    patientContextId);
                return new ConsolidationResult
                {
                    Success = false,
                    ErrorMessage = "No documents with extracted data found for this patient",
                    RetryCount = 0,
                    DocumentsProcessed = 0
                };
            }

            var extractedDataArray = documents
                .Where(doc => doc != null && doc.RootElement.ValueKind == JsonValueKind.Object)
                .Select(doc => doc!.RootElement)
                .ToList();

            if (extractedDataArray.Count == 0)
            {
                _logger.LogWarning(
                    "All documents have empty or invalid extractions for PatientContextId: {PatientContextId}",
                    patientContextId);
                return new ConsolidationResult
                {
                    Success = false,
                    ErrorMessage = "All documents have empty or invalid extractions",
                    RetryCount = 0,
                    DocumentsProcessed = 0
                };
            }

            // If there's only one document, still perform consolidation for consistency and proper structuring
            if (extractedDataArray.Count == 1)
            {
                _logger.LogInformation(
                    "Single document detected for PatientContextId: {PatientContextId}. Performing AI consolidation for consistency.",
                    patientContextId);
            }

            _logger.LogInformation(
                "Found {DocumentCount} documents with valid extractions for PatientContextId: {PatientContextId}",
                extractedDataArray.Count,
                patientContextId);

            var endpoint = _configuration["AzureOpenAI:Endpoint"];
            var deploymentName = _configuration["AzureOpenAI:DeploymentName"];
            var apiVersion = _configuration["AzureOpenAI:ApiVersion"];
            var apiKey = _configuration["AzureOpenAI:ApiKey"];

            if (string.IsNullOrWhiteSpace(endpoint) ||
                string.IsNullOrWhiteSpace(deploymentName) ||
                string.IsNullOrWhiteSpace(apiVersion))
            {
                _logger.LogError("Azure OpenAI configuration is incomplete");
                return new ConsolidationResult
                {
                    Success = false,
                    ErrorMessage = "Azure OpenAI configuration is incomplete",
                    RetryCount = 0
                };
            }

            if (string.IsNullOrWhiteSpace(apiKey))
            {
                _logger.LogError("Azure OpenAI API key is not configured");
                return new ConsolidationResult
                {
                    Success = false,
                    ErrorMessage = "Azure OpenAI API key is not configured",
                    RetryCount = 0
                };
            }

            var url = $"{endpoint}/openai/deployments/{deploymentName}/chat/completions?api-version={apiVersion}";

            for (int attempt = 0; attempt <= MaxRetries; attempt++)
            {
                try
                {
                    var requestPayload = CreateConsolidationRequestPayload(extractedDataArray, attempt);
                    
                    var content = new StringContent(
                        JsonSerializer.Serialize(requestPayload),
                        Encoding.UTF8,
                        "application/json");

                    var request = new HttpRequestMessage(HttpMethod.Post, url)
                    {
                        Content = content
                    };
                    request.Headers.Add("api-key", apiKey);

                    _logger.LogInformation(
                        "Sending consolidation request to Azure OpenAI for PatientContextId: {PatientContextId} (attempt {Attempt}/{MaxAttempts})",
                        patientContextId, attempt + 1, MaxRetries + 1);

                    var response = await _httpClient.SendAsync(request, cancellationToken);

                    if (response.IsSuccessStatusCode)
                    {
                        var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
                        
                        var result = ParseConsolidationResponse(responseContent, extractedDataArray.Count);

                        if (result.Success)
                        {
                            result.RetryCount = attempt;

                            patientContext.ConsolidatedData = result.ConsolidatedData;
                            
                            var conflictSection = _conflictService.ParseConflicts(result.ConsolidatedData);
                            var conflictCount = _conflictService.CountConflicts(conflictSection);
                            
                            patientContext.HasConflicts = conflictCount > 0;
                            patientContext.ConflictCount = conflictCount;
                            patientContext.Status = patientContext.HasConflicts ? "ConflictsPending" : "Consolidated";
                            patientContext.LastConsolidatedAt = DateTime.UtcNow;

                            await _dbContext.SaveChangesAsync(cancellationToken);

                            _logger.LogInformation(
                                "Patient data consolidation completed successfully for PatientContextId: {PatientContextId} after {Attempts} attempt(s). Status: {Status}, ConflictCount: {ConflictCount}",
                                patientContextId, attempt + 1, patientContext.Status, conflictCount);

                            return result;
                        }
                        else
                        {
                            // Check if this is an empty content response from GPT-5-mini reasoning model
                            if (result.ErrorMessage == "Empty content in response" && attempt < MaxRetries)
                            {
                                var delaySeconds = RetryDelaysSeconds[attempt];
                                _logger.LogWarning(
                                    "Empty content response from GPT-5-mini (likely due to reasoning token consumption). Retrying in {DelaySeconds} seconds with increased tokens (attempt {Attempt}/{MaxAttempts})",
                                    delaySeconds, attempt + 1, MaxRetries + 1);

                                await Task.Delay(TimeSpan.FromSeconds(delaySeconds), cancellationToken);
                                continue;
                            }

                            _logger.LogError(
                                "Failed to parse Azure OpenAI consolidation response: {Error}",
                                result.ErrorMessage);
                            return result;
                        }
                    }

                    if (response.StatusCode == HttpStatusCode.TooManyRequests)
                    {
                        if (attempt < MaxRetries)
                        {
                            var delaySeconds = RetryDelaysSeconds[attempt];
                            _logger.LogWarning(
                                "Rate limit (429) encountered during consolidation. Retrying in {DelaySeconds} seconds (attempt {Attempt}/{MaxAttempts})",
                                delaySeconds, attempt + 1, MaxRetries);

                            await Task.Delay(TimeSpan.FromSeconds(delaySeconds), cancellationToken);
                            continue;
                        }
                        else
                        {
                            _logger.LogError(
                                "Rate limit (429) encountered during consolidation. Max retries ({MaxRetries}) exceeded",
                                MaxRetries);
                            return new ConsolidationResult
                            {
                                Success = false,
                                ErrorMessage = "Azure OpenAI rate limit exceeded after maximum retries",
                                RetryCount = attempt,
                                DocumentsProcessed = extractedDataArray.Count
                            };
                        }
                    }

                    if ((int)response.StatusCode >= 500 && (int)response.StatusCode < 600)
                    {
                        if (attempt < MaxRetries)
                        {
                            var delaySeconds = RetryDelaysSeconds[attempt];
                            _logger.LogWarning(
                                "Server error ({StatusCode}) encountered during consolidation. Retrying in {DelaySeconds} seconds (attempt {Attempt}/{MaxAttempts})",
                                response.StatusCode, delaySeconds, attempt + 1, MaxRetries);

                            await Task.Delay(TimeSpan.FromSeconds(delaySeconds), cancellationToken);
                            continue;
                        }
                    }

                    var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                    _logger.LogError(
                        "Azure OpenAI consolidation request failed with status {StatusCode}: {ErrorContent}",
                        response.StatusCode, errorContent);

                    return new ConsolidationResult
                    {
                        Success = false,
                        ErrorMessage = $"Azure OpenAI request failed with status {response.StatusCode}",
                        RetryCount = attempt,
                        DocumentsProcessed = extractedDataArray.Count
                    };
                }
                catch (OperationCanceledException)
                {
                    _logger.LogWarning("Patient data consolidation was cancelled for PatientContextId: {PatientContextId}", patientContextId);
                    return new ConsolidationResult
                    {
                        Success = false,
                        ErrorMessage = "Consolidation was cancelled",
                        RetryCount = attempt,
                        DocumentsProcessed = extractedDataArray.Count
                    };
                }
                catch (HttpRequestException ex)
                {
                    if (attempt < MaxRetries)
                    {
                        var delaySeconds = RetryDelaysSeconds[attempt];
                        _logger.LogWarning(ex,
                            "HTTP request exception during consolidation. Retrying in {DelaySeconds} seconds (attempt {Attempt}/{MaxAttempts})",
                            delaySeconds, attempt + 1, MaxRetries);

                        await Task.Delay(TimeSpan.FromSeconds(delaySeconds), cancellationToken);
                        continue;
                    }

                    _logger.LogError(ex, "HTTP request failed after {MaxRetries} retries during consolidation", MaxRetries);
                    return new ConsolidationResult
                    {
                        Success = false,
                        ErrorMessage = "Failed to connect to Azure OpenAI service",
                        RetryCount = attempt,
                        DocumentsProcessed = extractedDataArray.Count
                    };
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unexpected error during patient data consolidation for PatientContextId: {PatientContextId}", patientContextId);
                    return new ConsolidationResult
                    {
                        Success = false,
                        ErrorMessage = "Unexpected error during consolidation",
                        RetryCount = attempt,
                        DocumentsProcessed = extractedDataArray.Count
                    };
                }
            }

            return new ConsolidationResult
            {
                Success = false,
                ErrorMessage = "Consolidation failed after maximum retries",
                RetryCount = MaxRetries,
                DocumentsProcessed = extractedDataArray.Count
            };
        }

        private object CreateConsolidationRequestPayload(List<JsonElement> extractedDataArray, int attempt)
        {
            var documentsJson = JsonSerializer.Serialize(extractedDataArray, new JsonSerializerOptions
            {
                WriteIndented = false
            });

            // Increase tokens on retry attempts to handle GPT-5-mini reasoning token consumption
            var baseTokens = 16000;
            var retryBonus = attempt * 8000; // Add 8k tokens for each retry attempt
            var maxTokens = baseTokens + retryBonus;

            return new
            {
                messages = new[]
                {
                    new { role = "system", content = PromptTemplates.CONSOLIDATION_PROMPT },
                    new { role = "user", content = $"Consolidate these patient documents into a single master record:\n\n{documentsJson}" }
                },
                max_completion_tokens = maxTokens
            };
        }

        private ConsolidationResult ParseConsolidationResponse(string responseContent, int documentCount)
        {
            try
            {
                _logger.LogDebug("Parsing Azure OpenAI response for {DocumentCount} documents", documentCount);
                
                using var document = JsonDocument.Parse(responseContent);
                var root = document.RootElement;

                if (!root.TryGetProperty("choices", out var choices) || choices.GetArrayLength() == 0)
                {
                    _logger.LogError("Invalid response format: missing choices array");
                    return new ConsolidationResult
                    {
                        Success = false,
                        ErrorMessage = "Invalid response format: missing choices array",
                        DocumentsProcessed = documentCount
                    };
                }

                var firstChoice = choices[0];
                if (!firstChoice.TryGetProperty("message", out var message))
                {
                    _logger.LogError("Invalid response format: missing message");
                    return new ConsolidationResult
                    {
                        Success = false,
                        ErrorMessage = "Invalid response format: missing message",
                        DocumentsProcessed = documentCount
                    };
                }

                if (!message.TryGetProperty("content", out var content))
                {
                    _logger.LogError("Invalid response format: missing content");
                    return new ConsolidationResult
                    {
                        Success = false,
                        ErrorMessage = "Invalid response format: missing content",
                        DocumentsProcessed = documentCount
                    };
                }

                var contentString = content.GetString();
                _logger.LogDebug("Extracted content string length: {Length}", contentString?.Length ?? 0);
                
                if (string.IsNullOrWhiteSpace(contentString))
                {
                    _logger.LogError("Empty content in response");
                    return new ConsolidationResult
                    {
                        Success = false,
                        ErrorMessage = "Empty content in response",
                        DocumentsProcessed = documentCount
                    };
                }

                var consolidatedData = JsonDocument.Parse(contentString);

                var hasConflicts = false;
                if (consolidatedData.RootElement.TryGetProperty("Conflicts", out var conflictsProperty))
                {
                    if (conflictsProperty.ValueKind == JsonValueKind.Object)
                    {
                        foreach (var conflictCategory in conflictsProperty.EnumerateObject())
                        {
                            if (conflictCategory.Value.ValueKind == JsonValueKind.Array && conflictCategory.Value.GetArrayLength() > 0)
                            {
                                hasConflicts = true;
                                break;
                            }
                        }
                    }
                }

                var result = new ConsolidationResult
                {
                    Success = true,
                    ConsolidatedData = consolidatedData,
                    HasConflicts = hasConflicts,
                    DocumentsProcessed = documentCount
                };

                if (hasConflicts)
                {
                    _logger.LogInformation(
                        "Consolidation completed with conflicts detected. {DocumentCount} documents processed.",
                        documentCount);
                }
                else
                {
                    _logger.LogInformation(
                        "Consolidation completed successfully without conflicts. {DocumentCount} documents processed.",
                        documentCount);
                }

                return result;
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Failed to parse Azure OpenAI consolidation response JSON");
                return new ConsolidationResult
                {
                    Success = false,
                    ErrorMessage = "Failed to parse consolidation response JSON",
                    DocumentsProcessed = documentCount
                };
            }
        }

        

    }
}
