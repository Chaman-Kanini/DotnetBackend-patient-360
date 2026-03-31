using TrustFirstPlatform.Domain.Entities;

namespace TrustFirstPlatform.Application.Tests.Fixtures
{
    public static class SessionFixtures
    {
        public static UserSession ActiveSession(Guid userId, string tokenJti) => new UserSession
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            TokenJti = tokenJti,
            CreatedAt = DateTime.UtcNow.AddMinutes(-5),
            ExpiresAt = DateTime.UtcNow.AddMinutes(10),
            LastActivityAt = DateTime.UtcNow.AddMinutes(-1),
            IsRevoked = false,
            IpAddress = "127.0.0.1"
        };

        public static UserSession ExpiredSession(Guid userId, string tokenJti) => new UserSession
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            TokenJti = tokenJti,
            CreatedAt = DateTime.UtcNow.AddMinutes(-20),
            ExpiresAt = DateTime.UtcNow.AddMinutes(-5),
            LastActivityAt = DateTime.UtcNow.AddMinutes(-16),
            IsRevoked = false,
            IpAddress = "127.0.0.1"
        };

        public static UserSession RevokedSession(Guid userId, string tokenJti) => new UserSession
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            TokenJti = tokenJti,
            CreatedAt = DateTime.UtcNow.AddMinutes(-10),
            ExpiresAt = DateTime.UtcNow.AddMinutes(5),
            LastActivityAt = DateTime.UtcNow.AddMinutes(-2),
            IsRevoked = true,
            IpAddress = "127.0.0.1"
        };
    }
}
