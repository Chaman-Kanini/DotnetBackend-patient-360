using TrustFirstPlatform.Application.Models;
using TrustFirstPlatform.Domain.Entities;

namespace TrustFirstPlatform.Application.Services
{
    public interface IPatientChatbotService
    {
        Task<ChatbotResponse> AskQuestionAsync(Guid patientContextId, string question, CancellationToken cancellationToken = default);
        Task<ChatbotResponse> AskQuestionWithHistoryAsync(Guid userId, string? batchId, string question, CancellationToken cancellationToken = default);
        Task<List<ChatHistory>> GetChatHistoryAsync(Guid userId, string? batchId = null, CancellationToken cancellationToken = default);
    }
}
