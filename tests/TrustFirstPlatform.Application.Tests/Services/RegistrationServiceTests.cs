using Microsoft.EntityFrameworkCore;
using Moq;
using TrustFirstPlatform.Application.DTOs;
using TrustFirstPlatform.Application.Services;
using TrustFirstPlatform.Application.Tests.Helpers;
using TrustFirstPlatform.Domain.Entities;
using TrustFirstPlatform.Infrastructure.Data;
using TrustFirstPlatform.Infrastructure.Email;

namespace TrustFirstPlatform.Application.Tests.Services
{
    public class RegistrationServiceTests : IDisposable
    {
        private readonly AppDbContext _context;
        private readonly Mock<IAuditService> _mockAuditService;
        private readonly Mock<IEmailService> _mockEmailService;
        private readonly RegistrationService _service;
        private readonly string _testIpAddress = "192.168.1.1";

        public RegistrationServiceTests()
        {
            _context = TestDbContextFactory.CreateInMemoryContext();
            _mockAuditService = new Mock<IAuditService>();
            _mockEmailService = new Mock<IEmailService>();
            
            _service = new RegistrationService(
                _context,
                _mockAuditService.Object,
                _mockEmailService.Object
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
                // Ignore disposal errors
            }
        }

        #region TC-006: Public Registration

        [Fact]
        public async Task RegisterAsync_ValidData_CreatesUserWithPendingStatus()
        {
            // Arrange
            var request = new PublicRegistrationRequest(
                Email: "newuser@test.com",
                Password: "SecurePass123!",
                ConfirmPassword: "SecurePass123!",
                FirstName: "John",
                LastName: "Doe",
                PhoneNumber: "1234567890",
                Department: "IT"
            );

            // Act
            var result = await _service.RegisterAsync(request, _testIpAddress);

            // Assert
            Assert.True(result.Success);
            Assert.Contains("pending approval", result.Message.ToLower());
            Assert.NotNull(result.User);
            Assert.Equal("Pending", result.User.Status);
            
            var userInDb = await _context.Users.FirstOrDefaultAsync(u => u.Email == "newuser@test.com");
            Assert.NotNull(userInDb);
            Assert.Equal("Pending", userInDb.Status);
            Assert.False(userInDb.IsActive);
            Assert.Equal("StandardUser", userInDb.Role);
            
            _mockAuditService.Verify(x => x.LogAsync(
                "USER_REGISTERED", 
                It.IsAny<Guid>(), 
                It.IsAny<object>(), 
                _testIpAddress), Times.Once);
        }

        #endregion

        #region TC-019 to TC-021: Negative Test Cases

        [Fact]
        public async Task RegisterAsync_DuplicateEmail_ReturnsFailure()
        {
            // Arrange
            await SeedUserAsync("existing@test.com");
            var request = new PublicRegistrationRequest(
                Email: "existing@test.com",
                Password: "SecurePass123!",
                ConfirmPassword: "SecurePass123!",
                FirstName: "Duplicate",
                LastName: "User",
                PhoneNumber: null,
                Department: null
            );

            // Act
            var result = await _service.RegisterAsync(request, _testIpAddress);

            // Assert
            Assert.False(result.Success);
            Assert.Contains("already registered", result.Message);
        }

        [Fact]
        public async Task RegisterAsync_WeakPassword_ReturnsFailure()
        {
            // Arrange
            var request = new PublicRegistrationRequest(
                Email: "weakpass@test.com",
                Password: "123",
                ConfirmPassword: "123",
                FirstName: "Weak",
                LastName: "Password",
                PhoneNumber: null,
                Department: null
            );

            // Act
            var result = await _service.RegisterAsync(request, _testIpAddress);

            // Assert
            Assert.False(result.Success);
            Assert.Contains("complexity requirements", result.Message);
        }

        [Fact]
        public async Task RegisterAsync_MismatchedPasswords_ReturnsFailure()
        {
            // Arrange
            var request = new PublicRegistrationRequest(
                Email: "mismatch@test.com",
                Password: "SecurePass123!",
                ConfirmPassword: "DifferentPass456!",
                FirstName: "Mismatch",
                LastName: "Password",
                PhoneNumber: null,
                Department: null
            );

            // Act
            var result = await _service.RegisterAsync(request, _testIpAddress);

            // Assert
            Assert.False(result.Success);
            Assert.Contains("do not match", result.Message);
        }

        #endregion

        #region EC-007: Email Case Sensitivity

        [Fact]
        public async Task RegisterAsync_EmailDifferentCase_AllowsRegistration()
        {
            // Arrange
            await SeedUserAsync("test@example.com");
            var request = new PublicRegistrationRequest(
                Email: "TEST@EXAMPLE.COM",
                Password: "SecurePass123!",
                ConfirmPassword: "SecurePass123!",
                FirstName: "Test",
                LastName: "User",
                PhoneNumber: null,
                Department: null
            );

            // Act
            var result = await _service.RegisterAsync(request, _testIpAddress);

            // Assert
            Assert.True(result.Success);
            Assert.Contains("pending approval", result.Message);
        }

        #endregion

        #region Email Availability Check

        [Fact]
        public async Task ValidateEmailAvailabilityAsync_NewEmail_ReturnsTrue()
        {
            // Act
            var result = await _service.ValidateEmailAvailabilityAsync("available@test.com");

            // Assert
            Assert.True(result);
        }

        [Fact]
        public async Task ValidateEmailAvailabilityAsync_ExistingEmail_ReturnsFalse()
        {
            // Arrange
            await SeedUserAsync("taken@test.com");

            // Act
            var result = await _service.ValidateEmailAvailabilityAsync("taken@test.com");

            // Assert
            Assert.False(result);
        }

        #endregion

        #region Password Complexity Tests

        [Theory]
        [InlineData("Short1!", false)] // Too short
        [InlineData("nouppercase123!", false)] // No uppercase
        [InlineData("NOLOWERCASE123!", false)] // No lowercase
        [InlineData("NoDigitsHere!", false)] // No digits
        [InlineData("NoSpecialChar123", false)] // No special characters
        [InlineData("ValidPass123!", true)] // Valid password
        [InlineData("AnotherGood1@", true)] // Valid password
        public async Task RegisterAsync_PasswordComplexity_ValidatesCorrectly(string password, bool shouldSucceed)
        {
            // Arrange
            var request = new PublicRegistrationRequest(
                Email: $"test{Guid.NewGuid()}@test.com",
                Password: password,
                ConfirmPassword: password,
                FirstName: "Test",
                LastName: "User",
                PhoneNumber: null,
                Department: null
            );

            // Act
            var result = await _service.RegisterAsync(request, _testIpAddress);

            // Assert
            if (shouldSucceed)
            {
                Assert.True(result.Success);
            }
            else
            {
                Assert.False(result.Success);
                Assert.Contains("complexity", result.Message.ToLower());
            }
        }

        #endregion

        #region Admin Notification Tests

        [Fact]
        public async Task RegisterAsync_Success_NotifiesAdmins()
        {
            // Arrange
            await SeedAdminUserAsync("admin1@test.com");
            await SeedAdminUserAsync("admin2@test.com");
            
            var request = new PublicRegistrationRequest(
                Email: "notify@test.com",
                Password: "SecurePass123!",
                ConfirmPassword: "SecurePass123!",
                FirstName: "Notify",
                LastName: "Test",
                PhoneNumber: null,
                Department: null
            );

            // Act
            await _service.RegisterAsync(request, _testIpAddress);

            // Assert
            _mockEmailService.Verify(x => x.SendPendingApprovalNotificationAsync(
                It.IsAny<string>(),
                "notify@test.com"), Times.AtLeast(2));
        }

        #endregion

        #region Helper Methods

        private async Task<Guid> SeedUserAsync(string email)
        {
            var user = new User
            {
                Id = Guid.NewGuid(),
                Email = email,
                PasswordHash = PasswordHasher.HashPassword("TestPassword123!"),
                Role = "StandardUser",
                FirstName = "Test",
                LastName = "User",
                Status = "Active",
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                Profile = System.Text.Json.JsonDocument.Parse("{}")
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();
            return user.Id;
        }

        private async Task<Guid> SeedAdminUserAsync(string email)
        {
            var user = new User
            {
                Id = Guid.NewGuid(),
                Email = email,
                PasswordHash = PasswordHasher.HashPassword("TestPassword123!"),
                Role = "Admin",
                FirstName = "Admin",
                LastName = "User",
                Status = "Active",
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                Profile = System.Text.Json.JsonDocument.Parse("{}")
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();
            return user.Id;
        }

        #endregion
    }

    public record RegistrationResult(bool Success, string Message, UserDto? User = null);
}
