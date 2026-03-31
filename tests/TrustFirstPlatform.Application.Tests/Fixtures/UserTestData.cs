using System;
using System.Text.Json;
using TrustFirstPlatform.Application.DTOs;
using TrustFirstPlatform.Domain.Entities;
using TrustFirstPlatform.Application.Services;

namespace TrustFirstPlatform.Application.Tests.Fixtures
{
    public static class UserTestData
    {
        public static User CreateTestUser(
            string email = "test@example.com",
            string role = "StandardUser",
            string status = "Active",
            bool isActive = true)
        {
            return new User
            {
                Id = Guid.NewGuid(),
                Email = email,
                PasswordHash = PasswordHasher.HashPassword("TestPassword123!"),
                Role = role,
                FirstName = "Test",
                LastName = "User",
                Status = status,
                IsActive = isActive,
                CreatedAt = DateTime.UtcNow,
                Profile = JsonDocument.Parse("{}")
            };
        }

        public static User CreateAdminUser(string email = "admin@example.com")
        {
            return CreateTestUser(email, "Admin", "Active", true);
        }

        public static User CreatePendingUser(string email = "pending@example.com")
        {
            return CreateTestUser(email, "StandardUser", "Pending", false);
        }

        public static CreateUserRequest CreateValidUserRequest(
            string email = "newuser@test.com",
            string role = "StandardUser")
        {
            return new CreateUserRequest(
                Email: email,
                FirstName: "John",
                LastName: "Doe",
                Role: role,
                PhoneNumber: "1234567890",
                Department: "IT"
            );
        }

        public static PublicRegistrationRequest CreateValidRegistrationRequest(
            string email = "register@test.com")
        {
            return new PublicRegistrationRequest(
                Email: email,
                Password: "SecurePass123!",
                ConfirmPassword: "SecurePass123!",
                FirstName: "Jane",
                LastName: "Smith",
                PhoneNumber: "9876543210",
                Department: "HR"
            );
        }

        public static UpdateUserRequest CreateValidUpdateRequest()
        {
            return new UpdateUserRequest(
                FirstName: "Updated",
                LastName: "Name",
                PhoneNumber: "5555555555",
                Department: "Finance"
            );
        }

        public static UserFilterRequest CreateDefaultFilterRequest(
            int page = 1,
            int pageSize = 10)
        {
            return new UserFilterRequest(
                Page: page,
                PageSize: pageSize,
                SearchTerm: null,
                Role: null,
                Status: null
            );
        }
    }
}
