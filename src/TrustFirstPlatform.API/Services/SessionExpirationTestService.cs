using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TrustFirstPlatform.Infrastructure.Data;
using TrustFirstPlatform.Domain.Entities;

namespace TrustFirstPlatform.API.Services
{
    /// <summary>
    /// Service to test session expiration functionality
    /// This can be called manually or via an endpoint to verify session expiration works correctly
    /// </summary>
    public class SessionExpirationTestService
    {
        private readonly ILogger<SessionExpirationTestService> _logger;
        private readonly IServiceProvider _serviceProvider;

        public SessionExpirationTestService(
            ILogger<SessionExpirationTestService> logger,
            IServiceProvider serviceProvider)
        {
            _logger = logger;
            _serviceProvider = serviceProvider;
        }

        /// <summary>
        /// Creates a test session that will expire in 1 minute for testing purposes
        /// </summary>
        public async Task<Guid> CreateTestSessionAsync(Guid userId, string ipAddress = "127.0.0.1")
        {
            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var testSession = new UserSession
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                TokenJti = $"test-token-{Guid.NewGuid()}",
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddMinutes(1), // Expires in 1 minute
                LastActivityAt = DateTime.UtcNow.AddMinutes(-20), // Last activity 20 minutes ago (expired)
                IsRevoked = false,
                IpAddress = ipAddress
            };

            dbContext.UserSessions.Add(testSession);
            await dbContext.SaveChangesAsync();

            _logger.LogInformation("Created test session {SessionId} for user {UserId} that should expire immediately", testSession.Id, userId);
            
            return testSession.Id;
        }

        /// <summary>
        /// Checks if a specific session is expired
        /// </summary>
        public async Task<bool> IsSessionExpiredAsync(Guid sessionId)
        {
            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var session = await dbContext.UserSessions
                .FirstOrDefaultAsync(s => s.Id == sessionId);

            if (session == null)
            {
                return true; // Session doesn't exist, consider it expired
            }

            var isExpired = session.IsRevoked || session.LastActivityAt < DateTime.UtcNow.AddMinutes(-15);
            
            _logger.LogInformation("Session {SessionId} expired status: {IsExpired} (Revoked: {IsRevoked}, LastActivity: {LastActivity})", 
                sessionId, isExpired, session.IsRevoked, session.LastActivityAt);

            return isExpired;
        }

        /// <summary>
        /// Manually triggers session cleanup to test the background service
        /// </summary>
        public async Task<int> TriggerSessionCleanupAsync()
        {
            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var auditService = scope.ServiceProvider.GetRequiredService<TrustFirstPlatform.Application.Services.IAuditService>();

            var cutoffTime = DateTime.UtcNow.Subtract(TimeSpan.FromMinutes(15));
            
            // Find sessions that have been inactive for more than 15 minutes
            var expiredSessions = await dbContext.UserSessions
                .Where(s => !s.IsRevoked && s.LastActivityAt < cutoffTime)
                .Include(s => s.User)
                .ToListAsync();

            var cleanedUpCount = 0;

            foreach (var session in expiredSessions)
            {
                session.IsRevoked = true;
                
                // Log session expiration for audit purposes
                await auditService.LogAsync(
                    "SESSION_EXPIRED_MANUAL_TEST", 
                    session.UserId, 
                    new { 
                        SessionId = session.Id,
                        TokenJti = session.TokenJti,
                        LastActivityAt = session.LastActivityAt,
                        InactiveDuration = DateTime.UtcNow - session.LastActivityAt
                    }, 
                    session.IpAddress);
                
                cleanedUpCount++;
            }

            if (cleanedUpCount > 0)
            {
                await dbContext.SaveChangesAsync();
                _logger.LogInformation("Manually cleaned up {Count} expired sessions", cleanedUpCount);
            }
            else
            {
                _logger.LogInformation("No expired sessions found during manual cleanup");
            }

            return cleanedUpCount;
        }

        /// <summary>
        /// Gets session statistics for testing
        /// </summary>
        public async Task<object> GetSessionStatisticsAsync()
        {
            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var now = DateTime.UtcNow;
            var cutoffTime = now.Subtract(TimeSpan.FromMinutes(15));

            var totalSessions = await dbContext.UserSessions.CountAsync();
            var activeSessions = await dbContext.UserSessions
                .CountAsync(s => !s.IsRevoked && s.ExpiresAt > now && s.LastActivityAt > cutoffTime);
            var expiredSessions = await dbContext.UserSessions
                .CountAsync(s => !s.IsRevoked && s.LastActivityAt < cutoffTime);
            var revokedSessions = await dbContext.UserSessions
                .CountAsync(s => s.IsRevoked);

            return new
            {
                TotalSessions = totalSessions,
                ActiveSessions = activeSessions,
                ExpiredSessions = expiredSessions,
                RevokedSessions = revokedSessions,
                CurrentTime = now,
                CutoffTime = cutoffTime
            };
        }
    }
}
