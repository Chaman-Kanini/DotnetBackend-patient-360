using TrustFirstPlatform.Application.DTOs;

namespace TrustFirstPlatform.Application.Services
{
    public interface IPasswordResetService
    {
        Task<bool> RequestResetAsync(ForgotPasswordRequest request);
        Task<bool> ValidateTokenAsync(string token);
        Task<bool> ResetPasswordAsync(ResetPasswordRequest request);
    }
}
