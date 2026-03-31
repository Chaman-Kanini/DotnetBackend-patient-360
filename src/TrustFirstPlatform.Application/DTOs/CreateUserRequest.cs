using System.ComponentModel.DataAnnotations;

namespace TrustFirstPlatform.Application.DTOs
{
    public record CreateUserRequest(
        [Required, EmailAddress] string Email,
        [Required, MaxLength(100)] string FirstName,
        [Required, MaxLength(100)] string LastName,
        [MaxLength(20)] string? PhoneNumber,
        [Required] string Role,
        [MaxLength(100)] string? Department
    );
}
