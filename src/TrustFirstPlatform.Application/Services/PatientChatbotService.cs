using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using TrustFirstPlatform.Application.Models;
using TrustFirstPlatform.Infrastructure.Data;

namespace TrustFirstPlatform.Application.Services
{
    public class PatientChatbotService : IPatientChatbotService
    {
        private readonly AppDbContext _dbContext;
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        private readonly ILogger<PatientChatbotService> _logger;

        public PatientChatbotService(
            AppDbContext dbContext,
            HttpClient httpClient,
            IConfiguration configuration,
            ILogger<PatientChatbotService> logger)
        {
            _dbContext = dbContext;
            _httpClient = httpClient;
            _configuration = configuration;
            _logger = logger;
        }

        public async Task<ChatbotResponse> AskQuestionAsync(
            Guid patientContextId,
            string question,
            CancellationToken cancellationToken = default)
        {
            _logger.LogInformation(
                "Processing chatbot question for PatientContextId: {PatientContextId}",
                patientContextId);

            var patientContext = await _dbContext.PatientContexts
                .FirstOrDefaultAsync(pc => pc.Id == patientContextId, cancellationToken);

            if (patientContext == null)
            {
                _logger.LogWarning("Patient context not found: {PatientContextId}", patientContextId);
                return new ChatbotResponse
                {
                    Success = false,
                    Answer = "Patient context not found.",
                    ErrorMessage = "Patient context not found"
                };
            }

            if (patientContext.ConsolidatedData == null)
            {
                _logger.LogWarning(
                    "No consolidated data available for PatientContextId: {PatientContextId}",
                    patientContextId);
                return new ChatbotResponse
                {
                    Success = false,
                    Answer = "No consolidated patient data available. Please consolidate the data first.",
                    ErrorMessage = "No consolidated data available"
                };
            }

            var endpoint = _configuration["AzureOpenAI:Endpoint"];
            var deploymentName = _configuration["AzureOpenAI:DeploymentName"];
            var apiVersion = _configuration["AzureOpenAI:ApiVersion"];
            var apiKey = _configuration["AzureOpenAI:ApiKey"];

            if (string.IsNullOrWhiteSpace(endpoint) ||
                string.IsNullOrWhiteSpace(deploymentName) ||
                string.IsNullOrWhiteSpace(apiVersion) ||
                string.IsNullOrWhiteSpace(apiKey))
            {
                _logger.LogError("Azure OpenAI configuration is incomplete");
                return new ChatbotResponse
                {
                    Success = false,
                    Answer = "Chatbot service is not properly configured.",
                    ErrorMessage = "Azure OpenAI configuration is incomplete"
                };
            }

            try
            {
                var url = $"{endpoint}/openai/deployments/{deploymentName}/chat/completions?api-version={apiVersion}";

                var consolidatedDataJson = JsonSerializer.Serialize(
                    patientContext.ConsolidatedData,
                    new JsonSerializerOptions { WriteIndented = true });

                var systemPrompt = @"You are a helpful medical assistant AI that answers questions about patient data. 
You have access to consolidated patient medical records. 
Provide accurate, clear, and concise answers based solely on the provided patient data.
If the information is not available in the patient data, clearly state that.
Always maintain patient privacy and professionalism.
Format your responses in a clear, easy-to-read manner.";

                var userPrompt = $@"Based on the following consolidated patient data, please answer this question:

Question: {question}

Patient Data:
{consolidatedDataJson}

Please provide a clear and accurate answer based only on the information available in the patient data.";

                var requestPayload = new
                {
                    messages = new[]
                    {
                        new { role = "system", content = systemPrompt },
                        new { role = "user", content = userPrompt }
                    },
                    max_completion_tokens = 1000
                };

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
                    "Sending chatbot request to Azure OpenAI for PatientContextId: {PatientContextId}",
                    patientContextId);

                var response = await _httpClient.SendAsync(request, cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                    _logger.LogError(
                        "Azure OpenAI request failed with status {StatusCode}: {Error}",
                        response.StatusCode, errorContent);

                    return new ChatbotResponse
                    {
                        Success = false,
                        Answer = "I'm sorry, I encountered an error while processing your question. Please try again.",
                        ErrorMessage = $"Azure OpenAI request failed: {response.StatusCode}"
                    };
                }

                var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
                var jsonResponse = JsonDocument.Parse(responseContent);

                if (jsonResponse.RootElement.TryGetProperty("choices", out var choices) &&
                    choices.GetArrayLength() > 0)
                {
                    var firstChoice = choices[0];
                    if (firstChoice.TryGetProperty("message", out var message) &&
                        message.TryGetProperty("content", out var answerContent))
                    {
                        var answer = answerContent.GetString();
                        
                        if (string.IsNullOrWhiteSpace(answer))
                        {
                            _logger.LogWarning("Empty answer received from Azure OpenAI");
                            return new ChatbotResponse
                            {
                                Success = false,
                                Answer = "I'm sorry, I couldn't generate a response. Please try rephrasing your question.",
                                ErrorMessage = "Empty response from AI"
                            };
                        }

                        _logger.LogInformation(
                            "Successfully generated chatbot response for PatientContextId: {PatientContextId}",
                            patientContextId);

                        return new ChatbotResponse
                        {
                            Success = true,
                            Answer = answer,
                            Question = question
                        };
                    }
                }

                _logger.LogError("Unexpected response format from Azure OpenAI");
                return new ChatbotResponse
                {
                    Success = false,
                    Answer = "I'm sorry, I received an unexpected response. Please try again.",
                    ErrorMessage = "Unexpected response format"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing chatbot question for PatientContextId: {PatientContextId}", patientContextId);
                return new ChatbotResponse
                {
                    Success = false,
                    Answer = "I'm sorry, an unexpected error occurred. Please try again later.",
                    ErrorMessage = ex.Message
                };
            }
        }

        public async Task<ChatbotResponse> AskQuestionWithHistoryAsync(
            Guid userId,
            string? batchId,
            string question,
            CancellationToken cancellationToken = default)
        {
            _logger.LogInformation(
                "Processing chatbot question with history for UserId: {UserId}, BatchId: {BatchId}",
                userId, batchId ?? "None");

            var endpoint = _configuration["AzureOpenAI:Endpoint"];
            var deploymentName = _configuration["AzureOpenAI:DeploymentName"];
            var apiVersion = _configuration["AzureOpenAI:ApiVersion"];
            var apiKey = _configuration["AzureOpenAI:ApiKey"];

            if (string.IsNullOrWhiteSpace(endpoint) ||
                string.IsNullOrWhiteSpace(deploymentName) ||
                string.IsNullOrWhiteSpace(apiVersion) ||
                string.IsNullOrWhiteSpace(apiKey))
            {
                _logger.LogError("Azure OpenAI configuration is incomplete");
                return new ChatbotResponse
                {
                    Success = false,
                    Answer = "Chatbot service is not properly configured.",
                    ErrorMessage = "Azure OpenAI configuration is incomplete"
                };
            }

            try
            {
                // Step 1: Call RAG API to get relevant context from ChromaDB
                string context = "";
                if (!string.IsNullOrWhiteSpace(batchId))
                {
                    try
                    {
                        var ragApiUrl = _configuration["RagApi:BaseUrl"] ?? "https://rag-service-patient-360.onrender.com";
                        
                        // Strip "batch_" prefix if present, as RAG API adds it automatically
                        var cleanBatchId = batchId.StartsWith("batch_", StringComparison.OrdinalIgnoreCase) 
                            ? batchId.Substring(6) 
                            : batchId;
                        
                        _logger.LogInformation(
                            "Calling RAG API at {RagApiUrl} with BatchId: {BatchId} (cleaned: {CleanBatchId}), Question: {Question}",
                            ragApiUrl, batchId, cleanBatchId, question);
                        
                        var ragResponse = await _httpClient.PostAsJsonAsync(
                            $"{ragApiUrl}/api/qa/ask",
                            new
                            {
                                question = question,
                                batch_id = cleanBatchId,
                                top_k = 5
                            },
                            cancellationToken
                        );

                        _logger.LogInformation(
                            "RAG API response status: {StatusCode}",
                            ragResponse.StatusCode);

                        if (ragResponse.IsSuccessStatusCode)
                        {
                            var responseBody = await ragResponse.Content.ReadAsStringAsync(cancellationToken);
                            _logger.LogInformation("RAG API response body: {ResponseBody}", responseBody);
                            
                            var ragResult = JsonDocument.Parse(responseBody);
                            if (ragResult != null && ragResult.RootElement.TryGetProperty("answer", out var answerElement))
                            {
                                // RAG API already provides the answer, return it directly
                                var ragAnswer = answerElement.GetString();
                                
                                _logger.LogInformation(
                                    "RAG API returned answer of length: {AnswerLength}",
                                    ragAnswer?.Length ?? 0);
                                
                                // Save to chat history
                                var chatHistory = new Domain.Entities.ChatHistory
                                {
                                    Id = Guid.NewGuid(),
                                    UserId = userId,
                                    BatchId = batchId,
                                    Question = question,
                                    Answer = ragAnswer ?? "No answer generated",
                                    Timestamp = DateTime.UtcNow
                                };

                                _dbContext.ChatHistories.Add(chatHistory);
                                await _dbContext.SaveChangesAsync(cancellationToken);

                                _logger.LogInformation(
                                    "Successfully generated and saved RAG chatbot response for UserId: {UserId}",
                                    userId);

                                return new ChatbotResponse
                                {
                                    Success = true,
                                    Answer = ragAnswer ?? "No answer generated",
                                    Question = question
                                };
                            }
                        }
                        else
                        {
                            var errorBody = await ragResponse.Content.ReadAsStringAsync(cancellationToken);
                            _logger.LogWarning(
                                "RAG API returned status {StatusCode}. Error: {ErrorBody}. Falling back to Azure OpenAI without context.",
                                ragResponse.StatusCode, errorBody);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Error calling RAG API at {RagApiUrl}. Falling back to Azure OpenAI without context.", 
                            _configuration["RagApi:BaseUrl"] ?? "http://localhost:8000");
                    }
                }
                else
                {
                    _logger.LogInformation("No batchId provided, skipping RAG API call");
                }

                // Step 2: Fallback to Azure OpenAI if RAG API is not available or batchId is null
                var url = $"{endpoint}/openai/deployments/{deploymentName}/chat/completions?api-version={apiVersion}";

                var systemPrompt = @"You are a helpful medical assistant AI that answers questions about patient data. 
You have access to patient medical records from RAG (Retrieval-Augmented Generation) system. 
Provide accurate, clear, and concise answers based on the provided context.
If the information is not available in the context, clearly state that.
Always maintain patient privacy and professionalism.
Format your responses in a clear, easy-to-read manner.";

                var userPrompt = string.IsNullOrWhiteSpace(context) 
                    ? question 
                    : $"Context:\n{context}\n\nQuestion: {question}";

                var requestPayload = new
                {
                    messages = new[]
                    {
                        new { role = "system", content = systemPrompt },
                        new { role = "user", content = userPrompt }
                    },
                    max_completion_tokens = 1000
                };

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
                    "Sending chatbot request to Azure OpenAI for UserId: {UserId}",
                    userId);

                var response = await _httpClient.SendAsync(request, cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                    _logger.LogError(
                        "Azure OpenAI request failed with status {StatusCode}: {Error}",
                        response.StatusCode, errorContent);

                    return new ChatbotResponse
                    {
                        Success = false,
                        Answer = "I'm sorry, I encountered an error while processing your question. Please try again.",
                        ErrorMessage = $"Azure OpenAI request failed: {response.StatusCode}"
                    };
                }

                var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
                var jsonResponse = JsonDocument.Parse(responseContent);

                if (jsonResponse.RootElement.TryGetProperty("choices", out var choices) &&
                    choices.GetArrayLength() > 0)
                {
                    var firstChoice = choices[0];
                    if (firstChoice.TryGetProperty("message", out var message) &&
                        message.TryGetProperty("content", out var answerContent))
                    {
                        var answer = answerContent.GetString();
                        
                        if (string.IsNullOrWhiteSpace(answer))
                        {
                            _logger.LogWarning("Empty answer received from Azure OpenAI");
                            return new ChatbotResponse
                            {
                                Success = false,
                                Answer = "I'm sorry, I couldn't generate a response. Please try rephrasing your question.",
                                ErrorMessage = "Empty response from AI"
                            };
                        }

                        // Save to chat history
                        var chatHistory = new Domain.Entities.ChatHistory
                        {
                            Id = Guid.NewGuid(),
                            UserId = userId,
                            BatchId = batchId,
                            Question = question,
                            Answer = answer,
                            Timestamp = DateTime.UtcNow
                        };

                        _dbContext.ChatHistories.Add(chatHistory);
                        await _dbContext.SaveChangesAsync(cancellationToken);

                        _logger.LogInformation(
                            "Successfully generated and saved chatbot response for UserId: {UserId}",
                            userId);

                        return new ChatbotResponse
                        {
                            Success = true,
                            Answer = answer,
                            Question = question
                        };
                    }
                }

                _logger.LogError("Unexpected response format from Azure OpenAI");
                return new ChatbotResponse
                {
                    Success = false,
                    Answer = "I'm sorry, I received an unexpected response. Please try again.",
                    ErrorMessage = "Unexpected response format"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing chatbot question for UserId: {UserId}", userId);
                return new ChatbotResponse
                {
                    Success = false,
                    Answer = "I'm sorry, an unexpected error occurred. Please try again later.",
                    ErrorMessage = ex.Message
                };
            }
        }

        public async Task<List<Domain.Entities.ChatHistory>> GetChatHistoryAsync(
            Guid userId,
            string? batchId = null,
            CancellationToken cancellationToken = default)
        {
            _logger.LogInformation(
                "Retrieving chat history for UserId: {UserId}, BatchId: {BatchId}",
                userId, batchId ?? "All");

            var query = _dbContext.ChatHistories
                .Where(ch => ch.UserId == userId);

            if (!string.IsNullOrWhiteSpace(batchId))
            {
                query = query.Where(ch => ch.BatchId == batchId);
            }

            var history = await query
                .OrderBy(ch => ch.Timestamp)
                .ToListAsync(cancellationToken);

            _logger.LogInformation(
                "Retrieved {Count} chat history records for UserId: {UserId}",
                history.Count, userId);

            return history;
        }
    }
}
