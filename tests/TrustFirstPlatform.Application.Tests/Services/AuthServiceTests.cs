using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using TrustFirstPlatform.Application.DTOs;
using TrustFirstPlatform.Application.Services;
using TrustFirstPlatform.Application.Tests.Fixtures;
using TrustFirstPlatform.Application.Tests.Helpers;
using TrustFirstPlatform.Domain.Entities;
using TrustFirstPlatform.Infrastructure.Data;

namespace TrustFirstPlatform.Application.Tests.Services
{
    public class AuthServiceTests : IDisposable
    {
        private readonly AppDbContext _context;
        private readonly Mock<IAuditService> _mockAuditService;
        private readonly JwtTokenService _jwtTokenService;
        private readonly AuthService _authService;
        private readonly string _testIpAddress = "192.168.1.1";

        public AuthServiceTests()
        {
            _context = TestDbContextFactory.CreateInMemoryContext();
            _mockAuditService = new Mock<IAuditService>();
            
            var mockConfig = new Mock<IConfiguration>();
            mockConfig.Setup(x => x["JwtSettings:SecretKey"]).Returns("test-secret-key-minimum-32-characters-long-for-security");
            mockConfig.Setup(x => x["JwtSettings:Issuer"]).Returns("test-issuer");
            mockConfig.Setup(x => x["JwtSettings:Audience"]).Returns("test-audience");
            mockConfig.Setup(x => x["JwtSettings:ExpirationMinutes"]).Returns("60");
            
            _jwtTokenService = new JwtTokenService(mockConfig.Object);
            _authService = new AuthService(_context, _jwtTokenService, _mockAuditService.Object);
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
        public async Task LoginAsync_ValidCredentials_ReturnsSuccessWithToken()
        {
            var user = UserFixtures.ValidUser;
            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            var request = new LoginRequest { Email = "user@test.com", Password = "Test@1234" };

            var result = await _authService.LoginAsync(request, _testIpAddress);

            Assert.NotNull(result);
            Assert.NotNull(result.Token);
            Assert.NotNull(result.User);
            Assert.Equal(user.Email, result.User.Email);
            Assert.True(result.ExpiresAt > DateTime.UtcNow);
            
            var updatedUser = await _context.Users.FindAsync(user.Id);
            Assert.Equal(0, updatedUser!.FailedLoginAttempts);
            Assert.Null(updatedUser.LockoutEnd);
            Assert.NotNull(updatedUser.LastLoginAt);
            
            _mockAuditService.Verify(x => x.LogAsync("LOGIN_SUCCESS", user.Id, It.IsAny<object>(), _testIpAddress), Times.Once);
        }

        [Fact]
        public async Task LoginAsync_ValidCredentials_CreatesNewSession()
        {
            var user = UserFixtures.ValidUser;
            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            var request = new LoginRequest { Email = "user@test.com", Password = "Test@1234" };

            var result = await _authService.LoginAsync(request, _testIpAddress);

            var session = await _context.UserSessions.FirstOrDefaultAsync(s => s.UserId == user.Id);
            Assert.NotNull(session);
            Assert.False(session.IsRevoked);
            Assert.Equal(_testIpAddress, session.IpAddress);
            Assert.True(session.ExpiresAt > DateTime.UtcNow);
            Assert.Equal(15, (session.ExpiresAt - session.CreatedAt).TotalMinutes, 1);
        }

        [Fact]
        public async Task LoginAsync_InvalidPassword_IncrementsFailedAttempts()
        {
            var user = UserFixtures.ValidUser;
            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            var request = new LoginRequest { Email = "user@test.com", Password = "WrongPassword" };

            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => _authService.LoginAsync(request, _testIpAddress));

            // Get the latest state from the database
            var updatedUser = await _context.Users.FindAsync(user.Id);
            Assert.Equal(1, updatedUser!.FailedLoginAttempts);
            
            _mockAuditService.Verify(x => x.LogAsync("LOGIN_FAILED", user.Id, It.IsAny<object>(), _testIpAddress), Times.Once);
        }

        [Fact]
        public async Task LoginAsync_NonExistentUser_ThrowsUnauthorizedException()
        {
            var request = new LoginRequest { Email = "nonexistent@test.com", Password = "Test@1234" };

            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => _authService.LoginAsync(request, _testIpAddress));
            
            _mockAuditService.Verify(x => x.LogAsync("LOGIN_FAILED", null, It.IsAny<object>(), _testIpAddress), Times.Once);
        }

        [Fact]
        public async Task LoginAsync_FifthFailedAttempt_LocksAccount()
        {
            var user = UserFixtures.UserWith4FailedAttempts;
            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            var request = new LoginRequest { Email = "almostlocked@test.com", Password = "WrongPassword" };

            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => _authService.LoginAsync(request, _testIpAddress));

            // Get the latest state from the database
            var updatedUser = await _context.Users.FindAsync(user.Id);
            Assert.Equal(5, updatedUser!.FailedLoginAttempts);
            Assert.NotNull(updatedUser.LockoutEnd);
            Assert.True(updatedUser.LockoutEnd > DateTime.UtcNow);
            Assert.Equal(30, (updatedUser.LockoutEnd.Value - DateTime.UtcNow).TotalMinutes, 1);
            
            _mockAuditService.Verify(x => x.LogAsync("ACCOUNT_LOCKED", user.Id, It.IsAny<object>(), _testIpAddress), Times.Once);
        }

        [Fact]
        public async Task LoginAsync_LockedAccount_ThrowsUnauthorizedException()
        {
            var user = UserFixtures.LockedUser;
            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            var request = new LoginRequest { Email = "locked@test.com", Password = "Test@1234" };

            var exception = await Assert.ThrowsAsync<UnauthorizedAccessException>(() => _authService.LoginAsync(request, _testIpAddress));
            Assert.Contains("locked until", exception.Message);
            
            _mockAuditService.Verify(x => x.LogAsync("LOGIN_FAILED", user.Id, It.IsAny<object>(), _testIpAddress), Times.Once);
        }

        [Fact]
        public async Task LoginAsync_InactiveAccount_ThrowsUnauthorizedException()
        {
            var user = UserFixtures.InactiveUser;
            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            var request = new LoginRequest { Email = "inactive@test.com", Password = "Test@1234" };

            var exception = await Assert.ThrowsAsync<UnauthorizedAccessException>(() => _authService.LoginAsync(request, _testIpAddress));
            Assert.Contains("inactive", exception.Message);
            
            _mockAuditService.Verify(x => x.LogAsync("LOGIN_FAILED", user.Id, It.IsAny<object>(), _testIpAddress), Times.Once);
        }

        [Fact]
        public async Task LoginAsync_ConcurrentSession_InvalidatesPreviousSession()
        {
            var user = UserFixtures.ValidUser;
            _context.Users.Add(user);
            
            var existingSession = SessionFixtures.ActiveSession(user.Id, "existing-jti");
            _context.UserSessions.Add(existingSession);
            await _context.SaveChangesAsync();

            var request = new LoginRequest { Email = "user@test.com", Password = "Test@1234" };

            var result = await _authService.LoginAsync(request, _testIpAddress);

            // Get the latest state from the database
            var previousSession = await _context.UserSessions.FindAsync(existingSession.Id);
            Assert.True(previousSession!.IsRevoked);
            
            var activeSessions = await _context.UserSessions
                .Where(s => s.UserId == user.Id && !s.IsRevoked)
                .CountAsync();
            Assert.Equal(1, activeSessions);
        }

        [Fact]
        public async Task LoginAsync_SuccessfulLoginAfterLockout_ResetsFailedAttempts()
        {
            var user = UserFixtures.ValidUser;
            user.FailedLoginAttempts = 3;
            user.LockoutEnd = DateTime.UtcNow.AddMinutes(-1);
            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            var request = new LoginRequest { Email = "user@test.com", Password = "Test@1234" };

            var result = await _authService.LoginAsync(request, _testIpAddress);

            // Get the latest state from the database
            var updatedUser = await _context.Users.FindAsync(user.Id);
            Assert.Equal(0, updatedUser!.FailedLoginAttempts);
            Assert.Null(updatedUser.LockoutEnd);
        }

        [Fact]
        public async Task LogoutAsync_ValidSession_RevokesSession()
        {
            var user = UserFixtures.ValidUser;
            _context.Users.Add(user);
            
            var session = SessionFixtures.ActiveSession(user.Id, "test-jti");
            _context.UserSessions.Add(session);
            await _context.SaveChangesAsync();

            var result = await _authService.LogoutAsync(user.Id, "test-jti");

            Assert.True(result);
            var updatedSession = await _context.UserSessions.FindAsync(session.Id);
            Assert.True(updatedSession!.IsRevoked);
            
            _mockAuditService.Verify(x => x.LogAsync("LOGOUT", user.Id, It.IsAny<object>(), session.IpAddress), Times.Once);
        }

        [Fact]
        public async Task LogoutAsync_NonExistentSession_ReturnsFalse()
        {
            var userId = Guid.NewGuid();

            var result = await _authService.LogoutAsync(userId, "non-existent-jti");

            Assert.False(result);
        }

        [Fact]
        public async Task LogoutAsync_AlreadyRevokedSession_ReturnsFalse()
        {
            var user = UserFixtures.ValidUser;
            _context.Users.Add(user);
            
            var session = SessionFixtures.RevokedSession(user.Id, "revoked-jti");
            _context.UserSessions.Add(session);
            await _context.SaveChangesAsync();

            var result = await _authService.LogoutAsync(user.Id, "revoked-jti");

            Assert.False(result);
        }

        [Fact]
        public async Task ValidateSessionAsync_ActiveSession_ReturnsTrue()
        {
            var user = UserFixtures.ValidUser;
            _context.Users.Add(user);
            
            var session = SessionFixtures.ActiveSession(user.Id, "valid-jti");
            _context.UserSessions.Add(session);
            await _context.SaveChangesAsync();

            // Verify session was saved and check all conditions
            var savedSession = await _context.UserSessions.FirstOrDefaultAsync(s => s.TokenJti == "valid-jti");
            Assert.NotNull(savedSession);
            Assert.Equal(user.Id, savedSession.UserId);
            Assert.False(savedSession.IsRevoked);
            Assert.True(savedSession.ExpiresAt > DateTime.UtcNow, $"Session expired: ExpiresAt={savedSession.ExpiresAt}, Now={DateTime.UtcNow}");

            // Debug: Check what the query finds
            var now = DateTime.UtcNow;
            var debugSession = await _context.UserSessions
                .FirstOrDefaultAsync(s => s.UserId == user.Id && s.TokenJti == "valid-jti" && !s.IsRevoked && s.ExpiresAt > now);
            Assert.NotNull(debugSession);

            // Clear change tracker to avoid conflicts
            _context.ChangeTracker.Clear();

            var result = await _authService.ValidateSessionAsync(user.Id, "valid-jti");

            Assert.True(result);
            
            // Get the latest state from the database
            var updatedSession = await _context.UserSessions.FindAsync(session.Id);
            Assert.True(updatedSession!.LastActivityAt > session.LastActivityAt);
        }

        [Fact]
        public async Task ValidateSessionAsync_ExpiredSession_ReturnsFalse()
        {
            var user = UserFixtures.ValidUser;
            _context.Users.Add(user);
            
            var session = SessionFixtures.ExpiredSession(user.Id, "expired-jti");
            _context.UserSessions.Add(session);
            await _context.SaveChangesAsync();

            var result = await _authService.ValidateSessionAsync(user.Id, "expired-jti");

            Assert.False(result);
        }

        [Fact]
        public async Task ValidateSessionAsync_RevokedSession_ReturnsFalse()
        {
            var user = UserFixtures.ValidUser;
            _context.Users.Add(user);
            
            var session = SessionFixtures.RevokedSession(user.Id, "revoked-jti");
            _context.UserSessions.Add(session);
            await _context.SaveChangesAsync();

            var result = await _authService.ValidateSessionAsync(user.Id, "revoked-jti");

            Assert.False(result);
        }

        [Fact]
        public async Task ValidateSessionAsync_NonExistentSession_ReturnsFalse()
        {
            var userId = Guid.NewGuid();

            var result = await _authService.ValidateSessionAsync(userId, "non-existent-jti");

            Assert.False(result);
        }

        [Fact]
        public async Task InvalidateAllSessionsAsync_RevokesAllUserSessions()
        {
            var user = UserFixtures.ValidUser;
            _context.Users.Add(user);
            
            var session1 = SessionFixtures.ActiveSession(user.Id, "jti-1");
            var session2 = SessionFixtures.ActiveSession(user.Id, "jti-2");
            var session3 = SessionFixtures.ActiveSession(user.Id, "jti-3");
            _context.UserSessions.AddRange(session1, session2, session3);
            await _context.SaveChangesAsync();

            await _authService.InvalidateAllSessionsAsync(user.Id);

            // Get the latest state from the database
            var sessions = await _context.UserSessions.Where(s => s.UserId == user.Id).ToListAsync();
            Assert.All(sessions, s => Assert.True(s.IsRevoked));
        }

        [Fact]
        public async Task InvalidateAllSessionsAsync_OnlyRevokesTargetUserSessions()
        {
            var user1 = UserFixtures.ValidUser;
            var user2 = UserFixtures.AdminUser;
            _context.Users.AddRange(user1, user2);
            
            var session1 = SessionFixtures.ActiveSession(user1.Id, "jti-1");
            var session2 = SessionFixtures.ActiveSession(user2.Id, "jti-2");
            _context.UserSessions.AddRange(session1, session2);
            await _context.SaveChangesAsync();

            await _authService.InvalidateAllSessionsAsync(user1.Id);

            var user1Session = await _context.UserSessions.FindAsync(session1.Id);
            var user2Session = await _context.UserSessions.FindAsync(session2.Id);
            
            Assert.True(user1Session!.IsRevoked);
            Assert.False(user2Session!.IsRevoked);
        }
    }
}
