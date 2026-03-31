using System.ComponentModel.DataAnnotations;

namespace TrustFirstPlatform.Application.DTOs
{
    public record PublicRegistrationRequest(
        [Required, EmailAddress] string Email,
        [Required, MinLength(8)] string Password,
        [Required] string ConfirmPassword,
        [Required, MaxLength(100)] string FirstName,
        [Required, MaxLength(100)] string LastName,
        [MaxLength(20)] string? PhoneNumber,
        [MaxLength(100)] string? Department
    );
}
