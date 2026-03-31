using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using TrustFirstPlatform.Domain.Entities;
using TrustFirstPlatform.Infrastructure.Data;

namespace TrustFirstPlatform.Application.Services
{
    public class AuditService : IAuditService
    {
        private readonly AppDbContext _context;

        private readonly ILogger<AuditService> _logger;

        public AuditService(AppDbContext context, ILogger<AuditService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task LogAsync(string action, Guid? userId, object metadata, string ipAddress)
        {
            try
            {
                var auditLog = new AuditLog
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    Action = action,
                    OccurredAt = DateTime.UtcNow,
                    IpAddress = ipAddress,
                    Metadata = JsonSerializer.SerializeToDocument(metadata ?? new { })
                };

                _context.AuditLogs.Add(auditLog);
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to write audit log {Action} for user {UserId}", action, userId);
            }
        }

        public Task LogAsync(Guid? userId, string action, object metadata)
        {
            return LogAsync(action, userId, metadata, "unknown");
        }
    }
}
