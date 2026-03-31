using TrustFirstPlatform.Application.DTOs;

namespace TrustFirstPlatform.Application.Services
{
    public interface IAuthService
    {
        Task<LoginResponse> LoginAsync(LoginRequest request, string ipAddress);
        Task<bool> LogoutAsync(Guid userId, string tokenJti);
        Task<bool> ValidateSessionAsync(Guid userId, string tokenJti);
        Task InvalidateAllSessionsAsync(Guid userId);
    }
}
