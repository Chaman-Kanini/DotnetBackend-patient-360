using TrustFirstPlatform.Domain.Entities;
using TrustFirstPlatform.Application.Services;

namespace TrustFirstPlatform.Application.Tests.Fixtures
{
    public static class UserFixtures
    {
        public static User ValidUser => new User
        {
            Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            Email = "user@test.com",
            PasswordHash = PasswordHasher.HashPassword("Test@1234"),
            Role = "StandardUser",
            FailedLoginAttempts = 0,
            LockoutEnd = null,
            IsActive = true,
            Status = "Active",
            FirstName = "Test",
            LastName = "User",
            CreatedAt = DateTime.UtcNow.AddDays(-30),
            LastLoginAt = DateTime.UtcNow.AddDays(-1),
            Profile = System.Text.Json.JsonDocument.Parse("{}")
        };

        public static User LockedUser => new User
        {
            Id = Guid.Parse("22222222-2222-2222-2222-222222222222"),
            Email = "locked@test.com",
            PasswordHash = PasswordHasher.HashPassword("Test@1234"),
            Role = "StandardUser",
            FailedLoginAttempts = 5,
            LockoutEnd = DateTime.UtcNow.AddMinutes(30),
            IsActive = true,
            Status = "Active",
            FirstName = "Locked",
            LastName = "User",
            CreatedAt = DateTime.UtcNow.AddDays(-30),
            Profile = System.Text.Json.JsonDocument.Parse("{}")
        };

        public static User UserWith4FailedAttempts => new User
        {
            Id = Guid.Parse("33333333-3333-3333-3333-333333333333"),
            Email = "almostlocked@test.com",
            PasswordHash = PasswordHasher.HashPassword("Test@1234"),
            Role = "StandardUser",
            FailedLoginAttempts = 4,
            LockoutEnd = null,
            IsActive = true,
            Status = "Active",
            FirstName = "Almost",
            LastName = "Locked",
            CreatedAt = DateTime.UtcNow.AddDays(-30),
            Profile = System.Text.Json.JsonDocument.Parse("{}")
        };

        public static User InactiveUser => new User
        {
            Id = Guid.Parse("44444444-4444-4444-4444-444444444444"),
            Email = "inactive@test.com",
            PasswordHash = PasswordHasher.HashPassword("Test@1234"),
            Role = "StandardUser",
            FailedLoginAttempts = 0,
            LockoutEnd = null,
            IsActive = false,
            Status = "Inactive",
            FirstName = "Inactive",
            LastName = "User",
            CreatedAt = DateTime.UtcNow.AddDays(-30),
            Profile = System.Text.Json.JsonDocument.Parse("{}")
        };

        public static User AdminUser => new User
        {
            Id = Guid.Parse("55555555-5555-5555-5555-555555555555"),
            Email = "admin@test.com",
            PasswordHash = PasswordHasher.HashPassword("Admin@1234"),
            Role = "Admin",
            FailedLoginAttempts = 0,
            LockoutEnd = null,
            IsActive = true,
            Status = "Active",
            FirstName = "Admin",
            LastName = "User",
            CreatedAt = DateTime.UtcNow.AddDays(-60),
            LastLoginAt = DateTime.UtcNow.AddHours(-2),
            Profile = System.Text.Json.JsonDocument.Parse("{}")
        };
    }
}
