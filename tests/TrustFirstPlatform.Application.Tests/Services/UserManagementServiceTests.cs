using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using TrustFirstPlatform.Application.DTOs;
using TrustFirstPlatform.Application.Services;
using TrustFirstPlatform.Application.Tests.Helpers;
using TrustFirstPlatform.Domain.Entities;
using TrustFirstPlatform.Infrastructure.Data;
using TrustFirstPlatform.Infrastructure.Email;

namespace TrustFirstPlatform.Application.Tests.Services
{
    public class UserManagementServiceTests : IDisposable
    {
        private readonly AppDbContext _context;
        private readonly Mock<IAuditService> _mockAuditService;
        private readonly Mock<IEmailService> _mockEmailService;
        private readonly Mock<ILogger<UserManagementService>> _mockLogger;
        private readonly UserManagementService _service;
        private readonly string _testIpAddress = "192.168.1.1";
        private readonly Guid _adminId = Guid.NewGuid();

        public UserManagementServiceTests()
        {
            _context = TestDbContextFactory.CreateInMemoryContext();
            _mockAuditService = new Mock<IAuditService>();
            _mockEmailService = new Mock<IEmailService>();
            _mockLogger = new Mock<ILogger<UserManagementService>>();
            
            _service = new UserManagementService(
                _context,
                _mockAuditService.Object,
                _mockEmailService.Object,
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
                // Ignore disposal errors
            }
        }

        #region TC-001 to TC-005: Admin User Creation

        [Fact]
        public async Task CreateUserAsync_ValidData_CreatesUserSuccessfully()
        {
            // Arrange
            var request = new CreateUserRequest(
                Email: "newuser@test.com",
                FirstName: "John",
                LastName: "Doe",
                Role: "Admin",
                PhoneNumber: "1234567890",
                Department: "IT"
            );

            // Act
            var result = await _service.CreateUserAsync(request, _adminId, _testIpAddress);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("newuser@test.com", result.Email);
            Assert.Equal("John", result.FirstName);
            Assert.Equal("Doe", result.LastName);
            Assert.Equal("Admin", result.Role);
            Assert.Equal("Active", result.Status);
            
            var userInDb = await _context.Users.FirstOrDefaultAsync(u => u.Email == "newuser@test.com");
            Assert.NotNull(userInDb);
            Assert.True(userInDb.IsActive);
            
            _mockEmailService.Verify(x => x.SendWelcomeEmailAsync(
                It.IsAny<string>(), 
                It.IsAny<string>(), 
                It.IsAny<string>()), Times.Once);
            
            _mockAuditService.Verify(x => x.LogAsync(
                "USER_CREATED", 
                _adminId, 
                It.IsAny<object>(), 
                _testIpAddress), Times.Once);
        }

        [Fact]
        public async Task CreateUserAsync_AdminRole_CreatesAdminUser()
        {
            // Arrange
            var request = new CreateUserRequest(
                Email: "admin@test.com",
                FirstName: "Admin",
                LastName: "User",
                Role: "Admin",
                PhoneNumber: null,
                Department: null
            );

            // Act
            var result = await _service.CreateUserAsync(request, _adminId, _testIpAddress);

            // Assert
            Assert.Equal("Admin", result.Role);
            var userInDb = await _context.Users.FirstOrDefaultAsync(u => u.Email == "admin@test.com");
            Assert.Equal("Admin", userInDb!.Role);
        }

        [Fact]
        public async Task CreateUserAsync_StandardUserRole_CreatesStandardUser()
        {
            // Arrange
            var request = new CreateUserRequest(
                Email: "standard@test.com",
                FirstName: "Standard",
                LastName: "User",
                Role: "StandardUser",
                PhoneNumber: null,
                Department: null
            );

            // Act
            var result = await _service.CreateUserAsync(request, _adminId, _testIpAddress);

            // Assert
            Assert.Equal("StandardUser", result.Role);
            var userInDb = await _context.Users.FirstOrDefaultAsync(u => u.Email == "standard@test.com");
            Assert.Equal("StandardUser", userInDb!.Role);
        }

        [Fact]
        public async Task CreateUserAsync_GeneratedPassword_MeetsComplexityRequirements()
        {
            // Arrange
            var request = new CreateUserRequest(
                Email: "testpassword@test.com",
                FirstName: "Test",
                LastName: "Password",
                Role: "StandardUser",
                PhoneNumber: null,
                Department: null
            );

            string? capturedPassword = null;
            _mockEmailService.Setup(x => x.SendWelcomeEmailAsync(
                It.IsAny<string>(), 
                It.IsAny<string>(), 
                It.IsAny<string>()))
                .Callback<string, string, string>((email, name, password) => capturedPassword = password)
                .Returns(Task.CompletedTask);

            // Act
            await _service.CreateUserAsync(request, _adminId, _testIpAddress);

            // Assert
            Assert.NotNull(capturedPassword);
            Assert.True(capturedPassword.Length >= 12);
        }

        [Fact]
        public async Task CreateUserAsync_Success_SendsWelcomeEmail()
        {
            // Arrange
            var request = new CreateUserRequest(
                Email: "welcome@test.com",
                FirstName: "Welcome",
                LastName: "Test",
                Role: "StandardUser",
                PhoneNumber: null,
                Department: null
            );

            // Act
            await _service.CreateUserAsync(request, _adminId, _testIpAddress);

            // Assert
            _mockEmailService.Verify(x => x.SendWelcomeEmailAsync(
                "welcome@test.com",
                "Welcome",
                It.IsAny<string>()), Times.Once);
        }

        #endregion

        #region TC-017 to TC-025: Negative Test Cases

        [Fact]
        public async Task CreateUserAsync_DuplicateEmail_ThrowsException()
        {
            // Arrange
            await SeedUserAsync("existing@test.com", "Admin");
            var request = new CreateUserRequest(
                Email: "existing@test.com",
                FirstName: "Duplicate",
                LastName: "User",
                Role: "Admin",
                PhoneNumber: null,
                Department: null
            );

            // Act & Assert
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(
                () => _service.CreateUserAsync(request, _adminId, _testIpAddress));
            Assert.Contains("Email already registered", exception.Message);
        }

        [Fact]
        public async Task CreateUserAsync_InvalidRole_ThrowsException()
        {
            // Arrange
            var request = new CreateUserRequest(
                Email: "invalid@test.com",
                FirstName: "Invalid",
                LastName: "Role",
                Role: "SuperAdmin",
                PhoneNumber: null,
                Department: null
            );

            // Act & Assert
            var exception = await Assert.ThrowsAsync<ArgumentException>(
                () => _service.CreateUserAsync(request, _adminId, _testIpAddress));
            Assert.Contains("Invalid role", exception.Message);
        }

        [Fact]
        public async Task DeactivateUserAsync_NonExistentUser_ThrowsException()
        {
            // Arrange
            var nonExistentUserId = Guid.NewGuid();

            // Act & Assert
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(
                () => _service.DeactivateUserAsync(nonExistentUserId, "Test reason", _adminId, _testIpAddress));
            Assert.Contains("User not found", exception.Message);
        }

        [Fact]
        public async Task UpdateUserRoleAsync_NonExistentUser_ThrowsException()
        {
            // Arrange
            var nonExistentUserId = Guid.NewGuid();

            // Act & Assert
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(
                () => _service.UpdateUserRoleAsync(nonExistentUserId, "Admin", _adminId, _testIpAddress));
            Assert.Contains("User not found", exception.Message);
        }

        #endregion

        #region EC-001 to EC-009: Edge Cases

        [Fact]
        public async Task DeactivateUserAsync_SelfDeactivation_ThrowsException()
        {
            // Arrange
            var userId = await SeedUserAsync("self@test.com", "Admin");

            // Act & Assert
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(
                () => _service.DeactivateUserAsync(userId, "Self deactivation", userId, _testIpAddress));
            Assert.Contains("Cannot deactivate your own account", exception.Message);
        }

        [Fact]
        public async Task DeactivateUserAsync_LastAdmin_ThrowsException()
        {
            // Arrange
            var adminId = await SeedUserAsync("lastadmin@test.com", "Admin");

            // Act & Assert
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(
                () => _service.DeactivateUserAsync(adminId, "Deactivate last admin", _adminId, _testIpAddress));
            Assert.Contains("Cannot deactivate the last admin account", exception.Message);
        }

        [Fact]
        public async Task CreateUserAsync_EmailServiceFails_RollsBackTransaction()
        {
            // Arrange
            var request = new CreateUserRequest(
                Email: "emailfail@test.com",
                FirstName: "Email",
                LastName: "Fail",
                Role: "StandardUser",
                PhoneNumber: null,
                Department: null
            );

            _mockEmailService.Setup(x => x.SendWelcomeEmailAsync(
                It.IsAny<string>(), 
                It.IsAny<string>(), 
                It.IsAny<string>()))
                .ThrowsAsync(new InvalidOperationException("SMTP error"));

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => _service.CreateUserAsync(request, _adminId, _testIpAddress));

            var userInDb = await _context.Users.FirstOrDefaultAsync(u => u.Email == "emailfail@test.com");
            Assert.Null(userInDb);
        }

        [Fact]
        public async Task DeactivateUserAsync_WithActiveSessions_InvalidatesAllSessions()
        {
            // Arrange
            var userId = await SeedUserAsync("sessionuser@test.com", "StandardUser");
            await SeedActiveSessionAsync(userId);
            await SeedActiveSessionAsync(userId);

            // Act
            await _service.DeactivateUserAsync(userId, "Test deactivation", _adminId, _testIpAddress);

            // Assert
            var sessions = await _context.UserSessions.Where(s => s.UserId == userId).ToListAsync();
            Assert.All(sessions, s => Assert.True(s.IsRevoked));
        }

        [Fact]
        public async Task CreateUserAsync_WithOptionalFieldsNull_CreatesSuccessfully()
        {
            // Arrange
            var request = new CreateUserRequest(
                Email: "minimal@test.com",
                FirstName: "Minimal",
                LastName: "User",
                Role: "StandardUser",
                PhoneNumber: null,
                Department: null
            );

            // Act
            var result = await _service.CreateUserAsync(request, _adminId, _testIpAddress);

            // Assert
            Assert.Null(result.PhoneNumber);
            Assert.Null(result.Department);
        }

        [Fact]
        public async Task UpdateUserRoleAsync_WhileUserLoggedIn_UpdatesRole()
        {
            // Arrange
            var userId = await SeedUserAsync("rolechange@test.com", "StandardUser");
            await SeedActiveSessionAsync(userId);

            // Act
            var result = await _service.UpdateUserRoleAsync(userId, "Admin", _adminId, _testIpAddress);

            // Assert
            Assert.True(result);
            var user = await _context.Users.FindAsync(userId);
            Assert.Equal("Admin", user!.Role);
        }

        #endregion

        #region TC-006 to TC-016: Positive Test Cases (Continued)

        [Fact]
        public async Task ApproveUserAsync_PendingUser_ActivatesUser()
        {
            // Arrange
            var userId = await SeedUserAsync("pending@test.com", "StandardUser", "Pending");

            // Act
            var result = await _service.ApproveUserAsync(userId, _adminId, _testIpAddress);

            // Assert
            Assert.Equal("Active", result.Status);
            Assert.NotNull(result.ApprovedAt);
            
            _mockEmailService.Verify(x => x.SendAccountApprovedEmailAsync(
                "pending@test.com",
                It.IsAny<string>()), Times.Once);
        }

        [Fact]
        public async Task RejectUserAsync_PendingUser_RejectsUser()
        {
            // Arrange
            var userId = await SeedUserAsync("reject@test.com", "StandardUser", "Pending");

            // Act
            var result = await _service.RejectUserAsync(userId, "Not eligible", _adminId, _testIpAddress);

            // Assert
            Assert.True(result);
            var user = await _context.Users.FindAsync(userId);
            Assert.Equal("Rejected", user!.Status);
            
            _mockEmailService.Verify(x => x.SendAccountRejectedEmailAsync(
                "reject@test.com",
                It.IsAny<string>(),
                "Not eligible"), Times.Once);
        }

        [Fact]
        public async Task DeactivateUserAsync_ActiveUser_DeactivatesSuccessfully()
        {
            // Arrange
            var userId = await SeedUserAsync("deactivate@test.com", "StandardUser");

            // Act
            var result = await _service.DeactivateUserAsync(userId, "Policy violation", _adminId, _testIpAddress);

            // Assert
            Assert.True(result);
            var user = await _context.Users.FindAsync(userId);
            Assert.False(user!.IsActive);
            Assert.Equal("Inactive", user.Status);
            Assert.NotNull(user.DeactivatedAt);
            Assert.Equal("Policy violation", user.DeactivationReason);
        }

        [Fact]
        public async Task ReactivateUserAsync_InactiveUser_ReactivatesSuccessfully()
        {
            // Arrange
            var userId = await SeedUserAsync("reactivate@test.com", "StandardUser", "Inactive");

            // Act
            var result = await _service.ReactivateUserAsync(userId, _adminId, _testIpAddress);

            // Assert
            Assert.True(result);
            var user = await _context.Users.FindAsync(userId);
            Assert.True(user!.IsActive);
            Assert.Equal("Active", user.Status);
            Assert.Null(user.DeactivatedAt);
        }

        [Fact]
        public async Task UpdateUserAsync_ValidData_UpdatesSuccessfully()
        {
            // Arrange
            var userId = await SeedUserAsync("update@test.com", "StandardUser");
            var request = new UpdateUserRequest(
                FirstName: "Updated",
                LastName: "Name",
                PhoneNumber: "9876543210",
                Department: "HR"
            );

            // Act
            var result = await _service.UpdateUserAsync(userId, request, _adminId, _testIpAddress);

            // Assert
            Assert.Equal("Updated", result.FirstName);
            Assert.Equal("Name", result.LastName);
            Assert.Equal("9876543210", result.PhoneNumber);
            Assert.Equal("HR", result.Department);
        }

        [Fact]
        public async Task GetUsersAsync_WithPagination_ReturnsPaginatedResults()
        {
            // Arrange
            for (int i = 0; i < 25; i++)
            {
                await SeedUserAsync($"user{i}@test.com", "StandardUser");
            }

            var filter = new UserFilterRequest(
                Page: 1,
                PageSize: 10,
                SearchTerm: null,
                Role: null,
                Status: null
            );

            // Act
            var result = await _service.GetUsersAsync(filter);

            // Assert
            Assert.Equal(10, result.Items.Length);
            Assert.Equal(25, result.TotalCount);
            Assert.Equal(3, result.TotalPages);
        }

        [Fact]
        public async Task GetUsersAsync_FilterByRole_ReturnsFilteredUsers()
        {
            // Arrange
            await SeedUserAsync("admin1@test.com", "Admin");
            await SeedUserAsync("admin2@test.com", "Admin");
            await SeedUserAsync("user1@test.com", "StandardUser");

            var filter = new UserFilterRequest(
                Page: 1,
                PageSize: 10,
                SearchTerm: null,
                Role: "Admin",
                Status: null
            );

            // Act
            var result = await _service.GetUsersAsync(filter);

            // Assert
            Assert.Equal(2, result.TotalCount);
            Assert.All(result.Items, u => Assert.Equal("Admin", u.Role));
        }

        [Fact]
        public async Task GetUsersAsync_FilterByStatus_ReturnsFilteredUsers()
        {
            // Arrange
            await SeedUserAsync("active@test.com", "StandardUser", "Active");
            await SeedUserAsync("pending@test.com", "StandardUser", "Pending");

            var filter = new UserFilterRequest(
                Page: 1,
                PageSize: 10,
                SearchTerm: null,
                Role: null,
                Status: "Pending"
            );

            // Act
            var result = await _service.GetUsersAsync(filter);

            // Assert
            Assert.Equal(1, result.TotalCount);
            Assert.Equal("Pending", result.Items[0].Status);
        }

        [Fact]
        public async Task GetUsersAsync_SearchByEmail_ReturnsMatchingUsers()
        {
            // Arrange
            await SeedUserAsync("john.doe@test.com", "StandardUser");
            await SeedUserAsync("jane.smith@test.com", "StandardUser");

            var filter = new UserFilterRequest(
                Page: 1,
                PageSize: 10,
                SearchTerm: "john",
                Role: null,
                Status: null
            );

            // Act
            var result = await _service.GetUsersAsync(filter);

            // Assert
            Assert.Equal(1, result.TotalCount);
            Assert.Contains("john", result.Items[0].Email);
        }

        #endregion

        #region Helper Methods

        private async Task<Guid> SeedUserAsync(string email, string role, string status = "Active")
        {
            var user = new User
            {
                Id = Guid.NewGuid(),
                Email = email,
                PasswordHash = PasswordHasher.HashPassword("TestPassword123!"),
                Role = role,
                FirstName = "Test",
                LastName = "User",
                Status = status,
                IsActive = status == "Active",
                CreatedAt = DateTime.UtcNow,
                Profile = System.Text.Json.JsonDocument.Parse("{}")
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();
            return user.Id;
        }

        private async Task SeedActiveSessionAsync(Guid userId)
        {
            var session = new UserSession
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                TokenJti = Guid.NewGuid().ToString(),
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddHours(1),
                LastActivityAt = DateTime.UtcNow,
                IsRevoked = false,
                IpAddress = "192.168.1.1"
            };

            _context.UserSessions.Add(session);
            await _context.SaveChangesAsync();
        }

        #endregion
    }
}
