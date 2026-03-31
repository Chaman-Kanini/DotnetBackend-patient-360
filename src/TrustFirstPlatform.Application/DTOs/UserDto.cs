using System;

namespace TrustFirstPlatform.Application.DTOs
{
    public record UserDto(
        Guid Id,
        string Email,
        string FirstName,
        string LastName,
        string Role,
        string Status,
        DateTime CreatedAt,
        DateTime? LastLoginAt,
        string? PhoneNumber,
        string? Department,
        DateTime? ApprovedAt,
        DateTime? DeactivatedAt
    );
}
