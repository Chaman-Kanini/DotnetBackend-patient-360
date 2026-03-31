using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using TrustFirstPlatform.Application.Constants;
using TrustFirstPlatform.Application.Models;

namespace TrustFirstPlatform.Application.Services
{
    public class ClinicalExtractionService : IClinicalExtractionService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        private readonly ILogger<ClinicalExtractionService> _logger;
        private const int MaxRetries = 3;
        private static readonly int[] RetryDelaysSeconds = { 10, 20, 30 };

        public ClinicalExtractionService(
            HttpClient httpClient,
            IConfiguration configuration,
            ILogger<ClinicalExtractionService> logger)
        {
            _httpClient = httpClient;
            _configuration = configuration;
            _logger = logger;
        }

        public async Task<ClinicalExtractionResult> ExtractClinicalDataAsync(
            string extractedText, 
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(extractedText))
            {
                return new ClinicalExtractionResult
                {
                    Success = false,
                    ErrorMessage = "Extracted text is empty",
                    RetryCount = 0
                };
            }

            var endpoint = _configuration["AzureOpenAI:Endpoint"];
            var deploymentName = _configuration["AzureOpenAI:DeploymentName"];
            var apiVersion = _configuration["AzureOpenAI:ApiVersion"];
            var apiKey = _configuration["AzureOpenAI:ApiKey"];

            if (string.IsNullOrWhiteSpace(endpoint) || 
                string.IsNullOrWhiteSpace(deploymentName) || 
                string.IsNullOrWhiteSpace(apiVersion))
            {
                _logger.LogError("Azure OpenAI configuration is incomplete");
                return new ClinicalExtractionResult
                {
                    Success = false,
                    ErrorMessage = "Azure OpenAI configuration is incomplete",
                    RetryCount = 0
                };
            }

            if (string.IsNullOrWhiteSpace(apiKey))
            {
                _logger.LogError("Azure OpenAI API key is not configured");
                return new ClinicalExtractionResult
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
                    var requestPayload = CreateRequestPayload(extractedText);
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
                        "Sending clinical extraction request to Azure OpenAI (attempt {Attempt}/{MaxAttempts})",
                        attempt + 1, MaxRetries + 1);

                    var response = await _httpClient.SendAsync(request, cancellationToken);

                    if (response.IsSuccessStatusCode)
                    {
                        var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
                        
                        var result = ParseResponse(responseContent);
                        
                        if (result.Success)
                        {
                            result.RetryCount = attempt;
                            _logger.LogInformation(
                                "Clinical extraction completed successfully after {Attempts} attempt(s)",
                                attempt + 1);
                            return result;
                        }
                        else
                        {
                            _logger.LogError("Failed to parse Azure OpenAI response: {Error}", result.ErrorMessage);
                            return result;
                        }
                    }

                    if (response.StatusCode == HttpStatusCode.TooManyRequests)
                    {
                        if (attempt < MaxRetries)
                        {
                            var delaySeconds = RetryDelaysSeconds[attempt];
                            _logger.LogWarning(
                                "Rate limit (429) encountered. Retrying in {DelaySeconds} seconds (attempt {Attempt}/{MaxAttempts})",
                                delaySeconds, attempt + 1, MaxRetries);
                            
                            await Task.Delay(TimeSpan.FromSeconds(delaySeconds), cancellationToken);
                            continue;
                        }
                        else
                        {
                            _logger.LogError("Rate limit (429) encountered. Max retries ({MaxRetries}) exceeded", MaxRetries);
                            return new ClinicalExtractionResult
                            {
                                Success = false,
                                ErrorMessage = "Azure OpenAI rate limit exceeded after maximum retries",
                                RetryCount = attempt
                            };
                        }
                    }

                    if ((int)response.StatusCode >= 500 && (int)response.StatusCode < 600)
                    {
                        if (attempt < MaxRetries)
                        {
                            var delaySeconds = RetryDelaysSeconds[attempt];
                            _logger.LogWarning(
                                "Server error ({StatusCode}) encountered. Retrying in {DelaySeconds} seconds (attempt {Attempt}/{MaxAttempts})",
                                response.StatusCode, delaySeconds, attempt + 1, MaxRetries);
                            
                            await Task.Delay(TimeSpan.FromSeconds(delaySeconds), cancellationToken);
                            continue;
                        }
                    }

                    var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                    _logger.LogError(
                        "Azure OpenAI request failed with status {StatusCode}: {ErrorContent}",
                        response.StatusCode, errorContent);
                    
                    return new ClinicalExtractionResult
                    {
                        Success = false,
                        ErrorMessage = $"Azure OpenAI request failed with status {response.StatusCode}",
                        RetryCount = attempt
                    };
                }
                catch (OperationCanceledException)
                {
                    _logger.LogWarning("Clinical extraction was cancelled");
                    return new ClinicalExtractionResult
                    {
                        Success = false,
                        ErrorMessage = "Clinical extraction was cancelled",
                        RetryCount = attempt
                    };
                }
                catch (HttpRequestException ex)
                {
                    if (attempt < MaxRetries)
                    {
                        var delaySeconds = RetryDelaysSeconds[attempt];
                        _logger.LogWarning(ex,
                            "HTTP request exception. Retrying in {DelaySeconds} seconds (attempt {Attempt}/{MaxAttempts})",
                            delaySeconds, attempt + 1, MaxRetries);
                        
                        await Task.Delay(TimeSpan.FromSeconds(delaySeconds), cancellationToken);
                        continue;
                    }
                    
                    _logger.LogError(ex, "HTTP request failed after {MaxRetries} retries", MaxRetries);
                    return new ClinicalExtractionResult
                    {
                        Success = false,
                        ErrorMessage = "Failed to connect to Azure OpenAI service",
                        RetryCount = attempt
                    };
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unexpected error during clinical extraction");
                    return new ClinicalExtractionResult
                    {
                        Success = false,
                        ErrorMessage = "Unexpected error during clinical extraction",
                        RetryCount = attempt
                    };
                }
            }

            return new ClinicalExtractionResult
            {
                Success = false,
                ErrorMessage = "Clinical extraction failed after maximum retries",
                RetryCount = MaxRetries
            };
        }

        private object CreateRequestPayload(string extractedText)
        {
            return new
            {
                messages = new[]
                {
                    new { role = "system", content = PromptTemplates.CLINICAL_EXTRACTION_PROMPT },
                    new { role = "user", content = $"Extract clinical data from this document:\n\n{extractedText}" }
                },
                max_completion_tokens = 12000
            };
        }

        private ClinicalExtractionResult ParseResponse(string responseContent)
        {
            try
            {
                using var document = JsonDocument.Parse(responseContent);
                var root = document.RootElement;

                if (!root.TryGetProperty("choices", out var choices) || choices.GetArrayLength() == 0)
                {
                    _logger.LogError("Invalid response format: missing choices array");
                    return new ClinicalExtractionResult
                    {
                        Success = false,
                        ErrorMessage = "Invalid response format: missing choices array"
                    };
                }

                var firstChoice = choices[0];
                if (!firstChoice.TryGetProperty("message", out var message))
                {
                    _logger.LogError("Invalid response format: missing message");
                    return new ClinicalExtractionResult
                    {
                        Success = false,
                        ErrorMessage = "Invalid response format: missing message"
                    };
                }

                if (!message.TryGetProperty("content", out var content))
                {
                    _logger.LogError("Invalid response format: missing content");
                    return new ClinicalExtractionResult
                    {
                        Success = false,
                        ErrorMessage = "Invalid response format: missing content"
                    };
                }

                var contentString = content.GetString();
                if (string.IsNullOrWhiteSpace(contentString))
                {
                    _logger.LogError("Empty content in response");
                    return new ClinicalExtractionResult
                    {
                        Success = false,
                        ErrorMessage = "Empty content in response"
                    };
                }

                var extractedData = JsonDocument.Parse(contentString);
                
                // Validate the extracted clinical data structure and _source fields
                var validator = new ClinicalDataValidator();
                validator.ValidateExtractedData(extractedData);

                var result = new ClinicalExtractionResult
                {
                    Success = validator.IsValid,
                    ExtractedData = extractedData,
                    ValidationErrors = validator.Errors,
                    ValidationWarnings = validator.Warnings
                };

                if (!validator.IsValid)
                {
                    result.ErrorMessage = $"Validation failed: {string.Join(", ", validator.Errors)}";
                    _logger.LogWarning(
                        "Clinical data validation failed with {ErrorCount} errors and {WarningCount} warnings",
                        validator.Errors.Count, validator.Warnings.Count);
                }
                else if (validator.Warnings.Any())
                {
                    _logger.LogInformation(
                        "Clinical data extracted with {WarningCount} validation warnings",
                        validator.Warnings.Count);
                }

                return result;
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Failed to parse Azure OpenAI response JSON");
                return new ClinicalExtractionResult
                {
                    Success = false,
                    ErrorMessage = "Failed to parse response JSON"
                };
            }
        }
    }
}
