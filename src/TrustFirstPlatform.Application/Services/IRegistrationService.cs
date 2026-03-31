using System.Threading.Tasks;
using TrustFirstPlatform.Application.DTOs;

namespace TrustFirstPlatform.Application.Services
{
    public interface IRegistrationService
    {
        Task<RegistrationResult> RegisterAsync(PublicRegistrationRequest request, string ipAddress);
        Task<bool> ValidateEmailAvailabilityAsync(string email);
    }

    public record RegistrationResult(
        bool Success,
        string Message,
        UserDto? User = null
    );
}
