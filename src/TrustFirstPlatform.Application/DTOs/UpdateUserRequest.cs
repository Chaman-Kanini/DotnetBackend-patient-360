using System.ComponentModel.DataAnnotations;

namespace TrustFirstPlatform.Application.DTOs
{
    public record UpdateUserRequest(
        [MaxLength(100)] string? FirstName,
        [MaxLength(100)] string? LastName,
        [MaxLength(20)] string? PhoneNumber,
        [MaxLength(100)] string? Department
    );
}
