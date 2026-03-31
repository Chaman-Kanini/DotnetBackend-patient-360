using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using System.Security.Claims;
using TrustFirstPlatform.Application.DTOs;
using TrustFirstPlatform.Application.Services;
using TrustFirstPlatform.API.Controllers;

namespace TrustFirstPlatform.Application.Tests.Controllers
{
    public class UserManagementControllerTests
    {
        private readonly Mock<IUserManagementService> _mockService;
        private readonly UserManagementController _controller;
        private readonly Guid _adminId = Guid.NewGuid();

        public UserManagementControllerTests()
        {
            _mockService = new Mock<IUserManagementService>();
            _controller = new UserManagementController(_mockService.Object);

            // Setup HttpContext with admin claims
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, _adminId.ToString()),
                new Claim(ClaimTypes.Role, "Admin")
            };
            var identity = new ClaimsIdentity(claims, "TestAuth");
            var principal = new ClaimsPrincipal(identity);

            var httpContext = new DefaultHttpContext
            {
                User = principal,
                Connection = { RemoteIpAddress = System.Net.IPAddress.Parse("192.168.1.1") }
            };
            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = httpContext
            };
        }

        #region CreateUser

        [Fact]
        public async Task CreateUser_ValidRequest_ReturnsCreatedAtAction()
        {
            var request = new CreateUserRequest(
                Email: "new@test.com",
                FirstName: "New",
                LastName: "User",
                Role: "StandardUser",
                PhoneNumber: null,
                Department: null
            );
            var expectedUser = new UserDto(
                Guid.NewGuid(),
                "new@test.com",
                "New",
                "User",
                "StandardUser",
                "Active",
                DateTime.UtcNow,
                null,
                null,
                null,
                null,
                null
            );

            _mockService.Setup(s => s.CreateUserAsync(request, _adminId, It.IsAny<string>()))
                .ReturnsAsync(expectedUser);

            var result = await _controller.CreateUser(request);

            var createdResult = Assert.IsType<CreatedAtActionResult>(result.Result);
            Assert.Equal(201, createdResult.StatusCode);
            var user = Assert.IsType<UserDto>(createdResult.Value);
            Assert.Equal("new@test.com", user.Email);
        }

        [Fact]
        public async Task CreateUser_DuplicateEmail_ReturnsConflict()
        {
            var request = new CreateUserRequest(
                Email: "existing@test.com",
                FirstName: "Dup",
                LastName: "User",
                Role: "StandardUser",
                PhoneNumber: null,
                Department: null
            );

            _mockService.Setup(s => s.CreateUserAsync(request, _adminId, It.IsAny<string>()))
                .ThrowsAsync(new InvalidOperationException("Email already registered"));

            var result = await _controller.CreateUser(request);

            var conflictResult = Assert.IsType<ConflictObjectResult>(result.Result);
            Assert.Equal(409, conflictResult.StatusCode);
        }

        [Fact]
        public async Task CreateUser_InvalidRole_ReturnsBadRequest()
        {
            var request = new CreateUserRequest(
                Email: "user@test.com",
                FirstName: "Test",
                LastName: "User",
                Role: "InvalidRole",
                PhoneNumber: null,
                Department: null
            );

            _mockService.Setup(s => s.CreateUserAsync(request, _adminId, It.IsAny<string>()))
                .ThrowsAsync(new ArgumentException("Invalid role"));

            var result = await _controller.CreateUser(request);

            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result.Result);
            Assert.Equal(400, badRequestResult.StatusCode);
        }

        [Fact]
        public async Task CreateUser_ServiceException_Returns500()
        {
            var request = new CreateUserRequest(
                Email: "user@test.com",
                FirstName: "Test",
                LastName: "User",
                Role: "StandardUser",
                PhoneNumber: null,
                Department: null
            );

            _mockService.Setup(s => s.CreateUserAsync(request, _adminId, It.IsAny<string>()))
                .ThrowsAsync(new Exception("Unexpected error"));

            var result = await _controller.CreateUser(request);

            var statusResult = Assert.IsType<ObjectResult>(result.Result);
            Assert.Equal(500, statusResult.StatusCode);
        }

        #endregion

        #region GetUsers

        [Fact]
        public async Task GetUsers_ValidFilter_ReturnsOk()
        {
            var filter = new UserFilterRequest(null, null, null);
            var expectedResult = new PagedResult<UserDto>(
                Array.Empty<UserDto>(),
                0,
                1,
                10,
                0,
                false,
                false
            );

            _mockService.Setup(s => s.GetUsersAsync(filter))
                .ReturnsAsync(expectedResult);

            var result = await _controller.GetUsers(filter);

            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            Assert.Equal(200, okResult.StatusCode);
        }

        [Fact]
        public async Task GetUsers_ServiceException_Returns500()
        {
            var filter = new UserFilterRequest(null, null, null);

            _mockService.Setup(s => s.GetUsersAsync(filter))
                .ThrowsAsync(new Exception("Database error"));

            var result = await _controller.GetUsers(filter);

            var statusResult = Assert.IsType<ObjectResult>(result.Result);
            Assert.Equal(500, statusResult.StatusCode);
        }

        #endregion

        #region GetUserById

        [Fact]
        public async Task GetUserById_ExistingUser_ReturnsOk()
        {
            var userId = Guid.NewGuid();
            var expectedUser = new UserDto(
                userId,
                "user@test.com",
                "Test",
                "User",
                "StandardUser",
                "Active",
                DateTime.UtcNow,
                null,
                null,
                null,
                null,
                null
            );

            _mockService.Setup(s => s.GetUserByIdAsync(userId))
                .ReturnsAsync(expectedUser);

            var result = await _controller.GetUserById(userId);

            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var user = Assert.IsType<UserDto>(okResult.Value);
            Assert.Equal(userId, user.Id);
        }

        [Fact]
        public async Task GetUserById_NonExistentUser_ReturnsNotFound()
        {
            var userId = Guid.NewGuid();

            _mockService.Setup(s => s.GetUserByIdAsync(userId))
                .ReturnsAsync((UserDto?)null);

            var result = await _controller.GetUserById(userId);

            Assert.IsType<NotFoundObjectResult>(result.Result);
        }

        #endregion

        #region DeactivateUser

        [Fact]
        public async Task DeactivateUser_ValidRequest_ReturnsOk()
        {
            var userId = Guid.NewGuid();
            var request = new DeactivateUserRequest("Policy violation");

            _mockService.Setup(s => s.DeactivateUserAsync(userId, "Policy violation", _adminId, It.IsAny<string>()))
                .ReturnsAsync(true);

            var result = await _controller.DeactivateUser(userId, request);

            Assert.IsType<OkObjectResult>(result);
        }

        [Fact]
        public async Task DeactivateUser_InvalidOperation_ReturnsBadRequest()
        {
            var userId = Guid.NewGuid();
            var request = new DeactivateUserRequest("Test");

            _mockService.Setup(s => s.DeactivateUserAsync(userId, "Test", _adminId, It.IsAny<string>()))
                .ThrowsAsync(new InvalidOperationException("Cannot deactivate your own account"));

            var result = await _controller.DeactivateUser(userId, request);

            Assert.IsType<BadRequestObjectResult>(result);
        }

        #endregion

        #region ReactivateUser

        [Fact]
        public async Task ReactivateUser_ValidRequest_ReturnsOk()
        {
            var userId = Guid.NewGuid();

            _mockService.Setup(s => s.ReactivateUserAsync(userId, _adminId, It.IsAny<string>()))
                .ReturnsAsync(true);

            var result = await _controller.ReactivateUser(userId);

            Assert.IsType<OkObjectResult>(result);
        }

        [Fact]
        public async Task ReactivateUser_NonExistentUser_ReturnsNotFound()
        {
            var userId = Guid.NewGuid();

            _mockService.Setup(s => s.ReactivateUserAsync(userId, _adminId, It.IsAny<string>()))
                .ThrowsAsync(new InvalidOperationException("User not found"));

            var result = await _controller.ReactivateUser(userId);

            Assert.IsType<NotFoundObjectResult>(result);
        }

        #endregion

        #region ApproveUser

        [Fact]
        public async Task ApproveUser_ValidRequest_ReturnsOk()
        {
            var userId = Guid.NewGuid();
            var expectedUser = new UserDto(
                userId,
                "pending@test.com",
                "Pending",
                "User",
                "StandardUser",
                "Active",
                DateTime.UtcNow,
                null,
                null,
                null,
                DateTime.UtcNow,
                null
            );

            _mockService.Setup(s => s.ApproveUserAsync(userId, _adminId, It.IsAny<string>()))
                .ReturnsAsync(expectedUser);

            var result = await _controller.ApproveUser(userId);

            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var user = Assert.IsType<UserDto>(okResult.Value);
            Assert.Equal("Active", user.Status);
        }

        [Fact]
        public async Task ApproveUser_NonPendingUser_ReturnsBadRequest()
        {
            var userId = Guid.NewGuid();

            _mockService.Setup(s => s.ApproveUserAsync(userId, _adminId, It.IsAny<string>()))
                .ThrowsAsync(new InvalidOperationException("User is not pending approval"));

            var result = await _controller.ApproveUser(userId);

            Assert.IsType<BadRequestObjectResult>(result.Result);
        }

        #endregion

        #region RejectUser

        [Fact]
        public async Task RejectUser_ValidRequest_ReturnsOk()
        {
            var userId = Guid.NewGuid();
            var request = new RejectUserRequest("Not eligible");

            _mockService.Setup(s => s.RejectUserAsync(userId, "Not eligible", _adminId, It.IsAny<string>()))
                .ReturnsAsync(true);

            var result = await _controller.RejectUser(userId, request);

            Assert.IsType<OkObjectResult>(result);
        }

        [Fact]
        public async Task RejectUser_NonPendingUser_ReturnsBadRequest()
        {
            var userId = Guid.NewGuid();
            var request = new RejectUserRequest("Test");

            _mockService.Setup(s => s.RejectUserAsync(userId, "Test", _adminId, It.IsAny<string>()))
                .ThrowsAsync(new InvalidOperationException("User is not pending"));

            var result = await _controller.RejectUser(userId, request);

            Assert.IsType<BadRequestObjectResult>(result);
        }

        #endregion

        #region UpdateUserRole

        [Fact]
        public async Task UpdateUserRole_ValidRequest_ReturnsOk()
        {
            var userId = Guid.NewGuid();
            var request = new UpdateRoleRequest("Admin");

            _mockService.Setup(s => s.UpdateUserRoleAsync(userId, "Admin", _adminId, It.IsAny<string>()))
                .ReturnsAsync(true);

            var result = await _controller.UpdateUserRole(userId, request);

            Assert.IsType<OkObjectResult>(result);
        }

        [Fact]
        public async Task UpdateUserRole_InvalidOperation_ReturnsBadRequest()
        {
            var userId = Guid.NewGuid();
            var request = new UpdateRoleRequest("StandardUser");

            _mockService.Setup(s => s.UpdateUserRoleAsync(userId, "StandardUser", _adminId, It.IsAny<string>()))
                .ThrowsAsync(new InvalidOperationException("Cannot remove admin role from last admin"));

            var result = await _controller.UpdateUserRole(userId, request);

            Assert.IsType<BadRequestObjectResult>(result);
        }

        #endregion

        #region GetPendingUsers

        [Fact]
        public async Task GetPendingUsers_ReturnsOk()
        {
            var pendingUsers = new[]
            {
                new UserDto(Guid.NewGuid(), "pending@test.com", "Pending", "User", "StandardUser", "Pending", DateTime.UtcNow, null, null, null, null, null)
            };

            _mockService.Setup(s => s.GetPendingUsersAsync())
                .ReturnsAsync(pendingUsers);

            var result = await _controller.GetPendingUsers();

            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var users = Assert.IsType<UserDto[]>(okResult.Value);
            Assert.Single(users);
        }

        [Fact]
        public async Task GetPendingUsers_ServiceException_Returns500()
        {
            _mockService.Setup(s => s.GetPendingUsersAsync())
                .ThrowsAsync(new Exception("Database error"));

            var result = await _controller.GetPendingUsers();

            var statusResult = Assert.IsType<ObjectResult>(result.Result);
            Assert.Equal(500, statusResult.StatusCode);
        }

        #endregion
    }
}
