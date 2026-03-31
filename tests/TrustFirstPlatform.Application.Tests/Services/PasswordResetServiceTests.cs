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
using TrustFirstPlatform.Infrastructure.Email;

namespace TrustFirstPlatform.Application.Tests.Services
{
    public class PasswordResetServiceTests : IDisposable
    {
        private readonly AppDbContext _context;
        private readonly Mock<IAuditService> _mockAuditService;
        private readonly Mock<IAuthService> _mockAuthService;
        private readonly Mock<IEmailService> _mockEmailService;
        private readonly Mock<IConfiguration> _mockConfiguration;
        private readonly Mock<ILogger<PasswordResetService>> _mockLogger;
        private readonly PasswordResetService _passwordResetService;

        public PasswordResetServiceTests()
        {
            _context = TestDbContextFactory.CreateInMemoryContext();
            _mockAuditService = new Mock<IAuditService>();
            _mockAuthService = new Mock<IAuthService>();
            _mockEmailService = new Mock<IEmailService>();
            _mockConfiguration = new Mock<IConfiguration>();
            _mockLogger = new Mock<ILogger<PasswordResetService>>();

            _mockConfiguration.Setup(x => x["App:FrontendBaseUrl"]).Returns("http://localhost:3000");
            _mockEmailService.Setup(x => x.SendPasswordResetEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .Returns(Task.CompletedTask);

            _passwordResetService = new PasswordResetService(
                _context,
                _mockAuditService.Object,
                _mockAuthService.Object,
                _mockEmailService.Object,
                _mockConfiguration.Object,
                _mockLogger.Object
            );
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
        public async Task RequestResetAsync_ValidEmail_ReturnsTrue()
        {
            var user = UserFixtures.ValidUser;
            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            var request = new ForgotPasswordRequest { Email = "user@test.com" };

            var result = await _passwordResetService.RequestResetAsync(request);

            Assert.True(result);
            
            // Get the latest state from the database
            var token = await _context.PasswordResetTokens.FirstOrDefaultAsync(t => t.UserId == user.Id);
            Assert.NotNull(token);
            Assert.False(token.IsUsed);
            Assert.True(token.ExpiresAt > DateTime.UtcNow);
            Assert.Equal(1, (token.ExpiresAt - DateTime.UtcNow).TotalHours, 0.1);
            
            _mockAuditService.Verify(x => x.LogAsync("PASSWORD_RESET_REQUESTED", user.Id, It.IsAny<object>(), "System"), Times.Once);
            _mockEmailService.Verify(x => x.SendPasswordResetEmailAsync(user.Email, It.IsAny<string>(), "http://localhost:3000"), Times.Once);
        }

        [Fact]
        public async Task RequestResetAsync_NonExistentEmail_ReturnsTrueWithoutCreatingToken()
        {
            var request = new ForgotPasswordRequest { Email = "nonexistent@test.com" };

            var result = await _passwordResetService.RequestResetAsync(request);

            Assert.True(result);
            
            var tokenCount = await _context.PasswordResetTokens.CountAsync();
            Assert.Equal(0, tokenCount);
            
            _mockAuditService.Verify(x => x.LogAsync(It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<object>(), It.IsAny<string>()), Times.Never);
            _mockEmailService.Verify(x => x.SendPasswordResetEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task RequestResetAsync_InvalidatesExistingTokens()
        {
            var user = UserFixtures.ValidUser;
            _context.Users.Add(user);
            
            var existingToken = new PasswordResetToken
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                Token = "existing-token",
                ExpiresAt = DateTime.UtcNow.AddHours(1),
                IsUsed = false
            };
            _context.PasswordResetTokens.Add(existingToken);
            await _context.SaveChangesAsync();

            var request = new ForgotPasswordRequest { Email = "user@test.com" };

            var result = await _passwordResetService.RequestResetAsync(request);

            Assert.True(result);
            
            var updatedToken = await _context.PasswordResetTokens.FindAsync(existingToken.Id);
            Assert.True(updatedToken!.IsUsed);
            
            var activeTokens = await _context.PasswordResetTokens
                .Where(t => t.UserId == user.Id && !t.IsUsed)
                .CountAsync();
            Assert.Equal(1, activeTokens);
        }

        [Fact]
        public async Task ValidateTokenAsync_ValidToken_ReturnsTrue()
        {
            var user = UserFixtures.ValidUser;
            _context.Users.Add(user);
            
            var token = new PasswordResetToken
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                Token = "valid-token",
                ExpiresAt = DateTime.UtcNow.AddHours(1),
                IsUsed = false
            };
            _context.PasswordResetTokens.Add(token);
            await _context.SaveChangesAsync();

            var result = await _passwordResetService.ValidateTokenAsync("valid-token");

            Assert.True(result);
        }

        [Fact]
        public async Task ValidateTokenAsync_ExpiredToken_ReturnsFalse()
        {
            var user = UserFixtures.ValidUser;
            _context.Users.Add(user);
            
            var token = new PasswordResetToken
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                Token = "expired-token",
                ExpiresAt = DateTime.UtcNow.AddHours(-1),
                IsUsed = false
            };
            _context.PasswordResetTokens.Add(token);
            await _context.SaveChangesAsync();

            var result = await _passwordResetService.ValidateTokenAsync("expired-token");

            Assert.False(result);
        }

        [Fact]
        public async Task ValidateTokenAsync_UsedToken_ReturnsFalse()
        {
            var user = UserFixtures.ValidUser;
            _context.Users.Add(user);
            
            var token = new PasswordResetToken
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                Token = "used-token",
                ExpiresAt = DateTime.UtcNow.AddHours(1),
                IsUsed = true
            };
            _context.PasswordResetTokens.Add(token);
            await _context.SaveChangesAsync();

            var result = await _passwordResetService.ValidateTokenAsync("used-token");

            Assert.False(result);
        }

        [Fact]
        public async Task ValidateTokenAsync_NonExistentToken_ReturnsFalse()
        {
            var result = await _passwordResetService.ValidateTokenAsync("non-existent-token");

            Assert.False(result);
        }

        [Fact]
        public async Task ResetPasswordAsync_ValidToken_UpdatesPasswordAndReturnsTrue()
        {
            var user = UserFixtures.ValidUser;
            var oldPasswordHash = user.PasswordHash;
            _context.Users.Add(user);
            
            var token = new PasswordResetToken
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                Token = "valid-reset-token",
                ExpiresAt = DateTime.UtcNow.AddHours(1),
                IsUsed = false
            };
            _context.PasswordResetTokens.Add(token);
            await _context.SaveChangesAsync();

            var request = new ResetPasswordRequest
            {
                Token = "valid-reset-token",
                NewPassword = "NewPassword@123"
            };

            var result = await _passwordResetService.ResetPasswordAsync(request);

            Assert.True(result);
            
            // Get the latest state from the database
            var updatedUser = await _context.Users.FindAsync(user.Id);
            Assert.NotEqual(oldPasswordHash, updatedUser!.PasswordHash);
            Assert.True(PasswordHasher.VerifyPassword("NewPassword@123", updatedUser.PasswordHash));
            Assert.Equal(0, updatedUser.FailedLoginAttempts);
            Assert.Null(updatedUser.LockoutEnd);
            
            var updatedToken = await _context.PasswordResetTokens.FindAsync(token.Id);
            Assert.True(updatedToken!.IsUsed);
            
            _mockAuthService.Verify(x => x.InvalidateAllSessionsAsync(user.Id), Times.Once);
            _mockAuditService.Verify(x => x.LogAsync("PASSWORD_RESET_COMPLETED", user.Id, It.IsAny<object>(), "System"), Times.Once);
        }

        [Fact]
        public async Task ResetPasswordAsync_ExpiredToken_ReturnsFalse()
        {
            var user = UserFixtures.ValidUser;
            _context.Users.Add(user);
            
            var token = new PasswordResetToken
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                Token = "expired-token",
                ExpiresAt = DateTime.UtcNow.AddHours(-1),
                IsUsed = false
            };
            _context.PasswordResetTokens.Add(token);
            await _context.SaveChangesAsync();

            var request = new ResetPasswordRequest
            {
                Token = "expired-token",
                NewPassword = "NewPassword@123"
            };

            var result = await _passwordResetService.ResetPasswordAsync(request);

            Assert.False(result);
        }

        [Fact]
        public async Task ResetPasswordAsync_UsedToken_ReturnsFalse()
        {
            var user = UserFixtures.ValidUser;
            _context.Users.Add(user);
            
            var token = new PasswordResetToken
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                Token = "used-token",
                ExpiresAt = DateTime.UtcNow.AddHours(1),
                IsUsed = true
            };
            _context.PasswordResetTokens.Add(token);
            await _context.SaveChangesAsync();

            var request = new ResetPasswordRequest
            {
                Token = "used-token",
                NewPassword = "NewPassword@123"
            };

            var result = await _passwordResetService.ResetPasswordAsync(request);

            Assert.False(result);
        }

        [Fact]
        public async Task ResetPasswordAsync_NonExistentToken_ReturnsFalse()
        {
            var request = new ResetPasswordRequest
            {
                Token = "non-existent-token",
                NewPassword = "NewPassword@123"
            };

            var result = await _passwordResetService.ResetPasswordAsync(request);

            Assert.False(result);
        }

        [Fact]
        public async Task ResetPasswordAsync_ResetsFailedLoginAttemptsAndLockout()
        {
            var user = UserFixtures.LockedUser;
            _context.Users.Add(user);
            
            var token = new PasswordResetToken
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                Token = "reset-token",
                ExpiresAt = DateTime.UtcNow.AddHours(1),
                IsUsed = false
            };
            _context.PasswordResetTokens.Add(token);
            await _context.SaveChangesAsync();

            var request = new ResetPasswordRequest
            {
                Token = "reset-token",
                NewPassword = "NewPassword@123"
            };

            var result = await _passwordResetService.ResetPasswordAsync(request);

            Assert.True(result);
            
            var updatedUser = await _context.Users.FindAsync(user.Id);
            Assert.Equal(0, updatedUser!.FailedLoginAttempts);
            Assert.Null(updatedUser.LockoutEnd);
        }

        [Fact]
        public async Task ResetPasswordAsync_InvalidatesAllUserSessions()
        {
            var user = UserFixtures.ValidUser;
            _context.Users.Add(user);
            
            var token = new PasswordResetToken
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                Token = "reset-token",
                ExpiresAt = DateTime.UtcNow.AddHours(1),
                IsUsed = false
            };
            _context.PasswordResetTokens.Add(token);
            await _context.SaveChangesAsync();

            var request = new ResetPasswordRequest
            {
                Token = "reset-token",
                NewPassword = "NewPassword@123"
            };

            await _passwordResetService.ResetPasswordAsync(request);

            _mockAuthService.Verify(x => x.InvalidateAllSessionsAsync(user.Id), Times.Once);
        }

        [Fact]
        public async Task RequestResetAsync_EmailServiceFailure_StillReturnsTrue()
        {
            var user = UserFixtures.ValidUser;
            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            _mockEmailService.Setup(x => x.SendPasswordResetEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .ThrowsAsync(new Exception("Email service unavailable"));

            var request = new ForgotPasswordRequest { Email = "user@test.com" };

            var result = await _passwordResetService.RequestResetAsync(request);

            Assert.True(result);
            
            var token = await _context.PasswordResetTokens.FirstOrDefaultAsync(t => t.UserId == user.Id);
            Assert.NotNull(token);
        }
    }
}
