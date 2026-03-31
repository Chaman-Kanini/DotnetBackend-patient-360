using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TrustFirstPlatform.Infrastructure.Data;

namespace TrustFirstPlatform.API.Services
{
    public class SessionCleanupService : BackgroundService
    {
        private readonly ILogger<SessionCleanupService> _logger;
        private readonly IServiceProvider _serviceProvider;
        private readonly TimeSpan _cleanupInterval = TimeSpan.FromMinutes(5);
        private readonly TimeSpan _sessionTimeout = TimeSpan.FromMinutes(15);

        public SessionCleanupService(ILogger<SessionCleanupService> logger, IServiceProvider serviceProvider)
        {
            _logger = logger;
            _serviceProvider = serviceProvider;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Session cleanup service started");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await CleanupExpiredSessionsAsync(stoppingToken);
                    await Task.Delay(_cleanupInterval, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error occurred during session cleanup");
                    await Task.Delay(_cleanupInterval, stoppingToken);
                }
            }

            _logger.LogInformation("Session cleanup service stopped");
        }

        private async Task CleanupExpiredSessionsAsync(CancellationToken cancellationToken)
        {
            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var auditService = scope.ServiceProvider.GetRequiredService<TrustFirstPlatform.Application.Services.IAuditService>();

            var cutoffTime = DateTime.UtcNow.Subtract(_sessionTimeout);

            var expiredSessions = await dbContext.UserSessions
                .Where(s => !s.IsRevoked && s.LastActivityAt < cutoffTime)
                .ToListAsync(cancellationToken);

            if (expiredSessions.Count == 0)
            {
                return;
            }

            foreach (var session in expiredSessions)
            {
                session.IsRevoked = true;

                await auditService.LogAsync(
                    "SESSION_EXPIRED",
                    session.UserId,
                    new
                    {
                        SessionId = session.Id,
                        TokenJti = session.TokenJti,
                        LastActivityAt = session.LastActivityAt,
                        InactiveDuration = DateTime.UtcNow - session.LastActivityAt
                    },
                    session.IpAddress);
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Cleaned up {Count} expired sessions", expiredSessions.Count);
        }
    }
}
