using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using TrustFirstPlatform.Application.Services;
using TrustFirstPlatform.Application.Tests.Fixtures;
using TrustFirstPlatform.Application.Tests.Helpers;
using TrustFirstPlatform.Infrastructure.Data;

namespace TrustFirstPlatform.Application.Tests.Services
{
    public class AuditServiceTests : IDisposable
    {
        private readonly AppDbContext _context;
        private readonly Mock<ILogger<AuditService>> _mockLogger;
        private readonly AuditService _auditService;

        public AuditServiceTests()
        {
            _context = TestDbContextFactory.CreateInMemoryContext();
            _mockLogger = new Mock<ILogger<AuditService>>();
            _auditService = new AuditService(_context, _mockLogger.Object);
        }

        public void Dispose()
        {
            try
            {
                _context.Database.CloseConnection();
                _context.Dispose();
            }
            catch
            {
                // Ignore disposal errors during test cleanup
            }
        }

        [Fact]
        public async Task LogAsync_ValidData_CreatesAuditEntry()
        {
            var userId = Guid.NewGuid();
            var action = "LOGIN_SUCCESS";
            var metadata = new { SessionId = Guid.NewGuid(), Browser = "Chrome" };
            var ipAddress = "192.168.1.1";

            await _auditService.LogAsync(action, userId, metadata, ipAddress);

            var auditLog = await _context.AuditLogs.FirstOrDefaultAsync(a => a.Action == action);
            Assert.NotNull(auditLog);
            Assert.Equal(userId, auditLog.UserId);
            Assert.Equal(action, auditLog.Action);
            Assert.Equal(ipAddress, auditLog.IpAddress);
            Assert.NotNull(auditLog.Metadata);
            Assert.True(auditLog.OccurredAt <= DateTime.UtcNow);
        }

        [Fact]
        public async Task LogAsync_NullUserId_CreatesAuditEntryWithNullUser()
        {
            var action = "LOGIN_FAILED";
            var metadata = new { Email = "unknown@test.com", Reason = "User not found" };
            var ipAddress = "192.168.1.1";

            await _auditService.LogAsync(action, null, metadata, ipAddress);

            var auditLog = await _context.AuditLogs.FirstOrDefaultAsync(a => a.Action == action);
            Assert.NotNull(auditLog);
            Assert.Null(auditLog.UserId);
            Assert.Equal(action, auditLog.Action);
        }

        [Fact]
        public async Task LogAsync_LoginSuccess_ContainsCorrectMetadata()
        {
            var userId = Guid.NewGuid();
            var sessionId = Guid.NewGuid();
            var metadata = new { SessionId = sessionId };

            await _auditService.LogAsync("LOGIN_SUCCESS", userId, metadata, "127.0.0.1");

            var auditLog = await _context.AuditLogs.FirstOrDefaultAsync(a => a.Action == "LOGIN_SUCCESS");
            Assert.NotNull(auditLog);
            Assert.Contains("SessionId", auditLog.Metadata.RootElement.ToString());
        }

        [Fact]
        public async Task LogAsync_LoginFailed_ContainsFailedAttempts()
        {
            var userId = Guid.NewGuid();
            var metadata = new { FailedAttempts = 3 };

            await _auditService.LogAsync("LOGIN_FAILED", userId, metadata, "127.0.0.1");

            var auditLog = await _context.AuditLogs.FirstOrDefaultAsync(a => a.Action == "LOGIN_FAILED");
            Assert.NotNull(auditLog);
            Assert.Contains("FailedAttempts", auditLog.Metadata.RootElement.ToString());
        }

        [Fact]
        public async Task LogAsync_AccountLocked_ContainsLockoutDuration()
        {
            var userId = Guid.NewGuid();
            var metadata = new { FailedAttempts = 5, LockoutDuration = "30 minutes" };

            await _auditService.LogAsync("ACCOUNT_LOCKED", userId, metadata, "127.0.0.1");

            var auditLog = await _context.AuditLogs.FirstOrDefaultAsync(a => a.Action == "ACCOUNT_LOCKED");
            Assert.NotNull(auditLog);
            Assert.Contains("LockoutDuration", auditLog.Metadata.RootElement.ToString());
        }

        [Fact]
        public async Task LogAsync_PasswordResetRequested_ContainsEmail()
        {
            var userId = Guid.NewGuid();
            var metadata = new { Email = "user@test.com" };

            await _auditService.LogAsync("PASSWORD_RESET_REQUESTED", userId, metadata, "System");

            var auditLog = await _context.AuditLogs.FirstOrDefaultAsync(a => a.Action == "PASSWORD_RESET_REQUESTED");
            Assert.NotNull(auditLog);
            Assert.Contains("Email", auditLog.Metadata.RootElement.ToString());
        }

        [Fact]
        public async Task LogAsync_PasswordResetCompleted_DoesNotContainToken()
        {
            var userId = Guid.NewGuid();
            var metadata = new { TokenId = Guid.NewGuid() };

            await _auditService.LogAsync("PASSWORD_RESET_COMPLETED", userId, metadata, "System");

            var auditLog = await _context.AuditLogs.FirstOrDefaultAsync(a => a.Action == "PASSWORD_RESET_COMPLETED");
            Assert.NotNull(auditLog);
            
            // Check that there's no "Token" property (exact match), but "TokenId" exists
            var metadataString = auditLog.Metadata.RootElement.ToString();
            Assert.DoesNotContain("\"Token\":", metadataString);
            Assert.Contains("\"TokenId\":", metadataString);
        }

        [Fact]
        public async Task LogAsync_Logout_ContainsSessionId()
        {
            var userId = Guid.NewGuid();
            var metadata = new { SessionId = Guid.NewGuid() };

            await _auditService.LogAsync("LOGOUT", userId, metadata, "192.168.1.1");

            var auditLog = await _context.AuditLogs.FirstOrDefaultAsync(a => a.Action == "LOGOUT");
            Assert.NotNull(auditLog);
            Assert.Contains("SessionId", auditLog.Metadata.RootElement.ToString());
        }

        [Fact]
        public async Task LogAsync_MultipleEntries_AllPersisted()
        {
            var userId = Guid.NewGuid();

            await _auditService.LogAsync("LOGIN_SUCCESS", userId, new { }, "127.0.0.1");
            await _auditService.LogAsync("LOGOUT", userId, new { }, "127.0.0.1");
            await _auditService.LogAsync("LOGIN_SUCCESS", userId, new { }, "127.0.0.1");

            var auditLogs = await _context.AuditLogs.Where(a => a.UserId == userId).ToListAsync();
            Assert.Equal(3, auditLogs.Count);
        }

        [Fact]
        public async Task LogAsync_DatabaseError_LogsErrorAndDoesNotThrow()
        {
            _context.Dispose();

            var exception = await Record.ExceptionAsync(async () =>
                await _auditService.LogAsync("TEST_ACTION", Guid.NewGuid(), new { }, "127.0.0.1")
            );

            Assert.Null(exception);
        }

        [Fact]
        public async Task LogAsync_NullMetadata_CreatesEntryWithEmptyObject()
        {
            var userId = Guid.NewGuid();

            await _auditService.LogAsync("TEST_ACTION", userId, null!, "127.0.0.1");

            var auditLog = await _context.AuditLogs.FirstOrDefaultAsync(a => a.Action == "TEST_ACTION");
            Assert.NotNull(auditLog);
            Assert.NotNull(auditLog.Metadata);
        }

        [Fact]
        public async Task LogAsync_AlternativeSignature_CreatesAuditEntry()
        {
            var userId = Guid.NewGuid();
            var action = "TEST_ACTION";
            var metadata = new { Test = "Data" };

            await _auditService.LogAsync(userId, action, metadata);

            var auditLog = await _context.AuditLogs.FirstOrDefaultAsync(a => a.Action == action);
            Assert.NotNull(auditLog);
            Assert.Equal(userId, auditLog.UserId);
            Assert.Equal("unknown", auditLog.IpAddress);
        }
    }
}
