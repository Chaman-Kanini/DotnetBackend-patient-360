using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using TrustFirstPlatform.Infrastructure.Data;
using TrustFirstPlatform.Domain.Entities;

namespace TrustFirstPlatform.API.Services
{
    public class SessionCleanupService : BackgroundService
    {
        private readonly ILogger<SessionCleanupService> _logger;
        private readonly IServiceProvider _serviceProvider;
        private readonly TimeSpan _cleanupInterval = TimeSpan.FromMinutes(5); // Run every 5 minutes
        private readonly TimeSpan _sessionTimeout = TimeSpan.FromMinutes(15); // 15-minute inactivity timeout

        public SessionCleanupService(
            ILogger<SessionCleanupService> logger,
            IServiceProvider serviceProvider)
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
                    // Normal shutdown
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error occurred during session cleanup");
                    // Continue running even if cleanup fails
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
            
            // Find sessions that have been inactive for more than 15 minutes
            var expiredSessions = await dbContext.UserSessions
                .Where(s => !s.IsRevoked && s.LastActivityAt < cutoffTime)
                .Include(s => s.User)
                .ToListAsync(cancellationToken);

            if (expiredSessions.Any())
            {
                _logger.LogInformation("Found {Count} expired sessions to clean up", expiredSessions.Count);

                foreach (var session in expiredSessions)
                {
                    session.IsRevoked = true;
                    
                    // Log session expiration for audit purposes
                    await auditService.LogAsync(
                        "SESSION_EXPIRED", 
                        session.UserId, 
                        new { 
                            SessionId = session.Id,
                            TokenJti = session.TokenJti,
                            LastActivityAt = session.LastActivityAt,
                            InactiveDuration = DateTime.UtcNow - session.LastActivityAt
                        }, 
                        session.IpAddress);
                }

                await dbContext.SaveChangesAsync(cancellationToken);
                
                _logger.LogInformation("Successfully cleaned up {Count} expired sessions", expiredSessions.Count);
            }
            else
            {
                _logger.LogDebug("No expired sessions found during cleanup");
            }
        }
    }
}
