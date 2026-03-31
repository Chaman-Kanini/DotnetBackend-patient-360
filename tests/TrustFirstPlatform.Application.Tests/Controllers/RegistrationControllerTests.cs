using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using TrustFirstPlatform.Application.DTOs;
using TrustFirstPlatform.Application.Services;
using TrustFirstPlatform.API.Controllers;

namespace TrustFirstPlatform.Application.Tests.Controllers
{
    public class RegistrationControllerTests
    {
        private readonly Mock<IRegistrationService> _mockService;
        private readonly RegistrationController _controller;

        public RegistrationControllerTests()
        {
            _mockService = new Mock<IRegistrationService>();
            _controller = new RegistrationController(_mockService.Object);

            var httpContext = new DefaultHttpContext
            {
                Connection = { RemoteIpAddress = System.Net.IPAddress.Parse("192.168.1.1") }
            };
            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = httpContext
            };
        }

        #region Register

        [Fact]
        public async Task Register_ValidRequest_ReturnsOk()
        {
            var request = new PublicRegistrationRequest(
                Email: "new@test.com",
                Password: "SecurePass123!",
                ConfirmPassword: "SecurePass123!",
                FirstName: "New",
                LastName: "User",
                PhoneNumber: null,
                Department: null
            );

            var expectedResult = new RegistrationResult(true, "Registration submitted successfully. Your account is pending approval.");

            _mockService.Setup(s => s.RegisterAsync(request, It.IsAny<string>()))
                .ReturnsAsync(expectedResult);

            var result = await _controller.Register(request);

            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            Assert.Equal(200, okResult.StatusCode);
        }

        [Fact]
        public async Task Register_DuplicateEmail_ReturnsBadRequest()
        {
            var request = new PublicRegistrationRequest(
                Email: "existing@test.com",
                Password: "SecurePass123!",
                ConfirmPassword: "SecurePass123!",
                FirstName: "Dup",
                LastName: "User",
                PhoneNumber: null,
                Department: null
            );

            var expectedResult = new RegistrationResult(false, "Email is already registered");

            _mockService.Setup(s => s.RegisterAsync(request, It.IsAny<string>()))
                .ReturnsAsync(expectedResult);

            var result = await _controller.Register(request);

            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result.Result);
            Assert.Equal(400, badRequestResult.StatusCode);
        }

        [Fact]
        public async Task Register_WeakPassword_ReturnsBadRequest()
        {
            var request = new PublicRegistrationRequest(
                Email: "user@test.com",
                Password: "123",
                ConfirmPassword: "123",
                FirstName: "Weak",
                LastName: "Password",
                PhoneNumber: null,
                Department: null
            );

            var expectedResult = new RegistrationResult(false, "Password does not meet complexity requirements");

            _mockService.Setup(s => s.RegisterAsync(request, It.IsAny<string>()))
                .ReturnsAsync(expectedResult);

            var result = await _controller.Register(request);

            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result.Result);
            Assert.Equal(400, badRequestResult.StatusCode);
        }

        [Fact]
        public async Task Register_ServiceException_Returns500()
        {
            var request = new PublicRegistrationRequest(
                Email: "user@test.com",
                Password: "SecurePass123!",
                ConfirmPassword: "SecurePass123!",
                FirstName: "Test",
                LastName: "User",
                PhoneNumber: null,
                Department: null
            );

            _mockService.Setup(s => s.RegisterAsync(request, It.IsAny<string>()))
                .ThrowsAsync(new Exception("Unexpected error"));

            var result = await _controller.Register(request);

            var statusResult = Assert.IsType<ObjectResult>(result.Result);
            Assert.Equal(500, statusResult.StatusCode);
        }

        #endregion

        #region CheckEmailAvailability

        [Fact]
        public async Task CheckEmailAvailability_AvailableEmail_ReturnsTrue()
        {
            _mockService.Setup(s => s.ValidateEmailAvailabilityAsync("available@test.com"))
                .ReturnsAsync(true);

            var result = await _controller.CheckEmailAvailability("available@test.com");

            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            Assert.Equal(200, okResult.StatusCode);
        }

        [Fact]
        public async Task CheckEmailAvailability_TakenEmail_ReturnsFalse()
        {
            _mockService.Setup(s => s.ValidateEmailAvailabilityAsync("taken@test.com"))
                .ReturnsAsync(false);

            var result = await _controller.CheckEmailAvailability("taken@test.com");

            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            Assert.Equal(200, okResult.StatusCode);
        }

        [Fact]
        public async Task CheckEmailAvailability_EmptyEmail_ReturnsBadRequest()
        {
            var result = await _controller.CheckEmailAvailability("");

            Assert.IsType<BadRequestObjectResult>(result.Result);
        }

        [Fact]
        public async Task CheckEmailAvailability_NullEmail_ReturnsBadRequest()
        {
            var result = await _controller.CheckEmailAvailability(null!);

            Assert.IsType<BadRequestObjectResult>(result.Result);
        }

        [Fact]
        public async Task CheckEmailAvailability_ServiceException_Returns500()
        {
            _mockService.Setup(s => s.ValidateEmailAvailabilityAsync("error@test.com"))
                .ThrowsAsync(new Exception("Database error"));

            var result = await _controller.CheckEmailAvailability("error@test.com");

            var statusResult = Assert.IsType<ObjectResult>(result.Result);
            Assert.Equal(500, statusResult.StatusCode);
        }

        #endregion
    }
}
