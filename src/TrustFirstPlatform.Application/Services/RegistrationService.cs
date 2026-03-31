using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using TrustFirstPlatform.Application.DTOs;
using TrustFirstPlatform.Domain.Entities;
using TrustFirstPlatform.Infrastructure.Data;
using TrustFirstPlatform.Infrastructure.Email;

namespace TrustFirstPlatform.Application.Services
{
    public class RegistrationService : IRegistrationService
    {
        private readonly AppDbContext _context;
        private readonly IAuditService _auditService;
        private readonly IEmailService _emailService;

        public RegistrationService(
            AppDbContext context,
            IAuditService auditService,
            IEmailService emailService)
        {
            _context = context;
            _auditService = auditService;
            _emailService = emailService;
        }

        public async Task<RegistrationResult> RegisterAsync(PublicRegistrationRequest request, string ipAddress)
        {
            // Validate password confirmation
            if (request.Password != request.ConfirmPassword)
            {
                return new RegistrationResult(false, "Password and confirmation do not match");
            }

            // Validate password complexity (reuse from US_001)
            if (!IsPasswordComplex(request.Password))
            {
                return new RegistrationResult(false, "Password does not meet complexity requirements. Must be at least 8 characters with mixed case, numbers, and special characters.");
            }

            // Check email availability
            if (!await ValidateEmailAvailabilityAsync(request.Email))
            {
                return new RegistrationResult(false, "Email is already registered");
            }

            // Create new user with "Pending" status
            var user = new User
            {
                Id = Guid.NewGuid(),
                Email = request.Email,
                PasswordHash = PasswordHasher.HashPassword(request.Password),
                Role = "StandardUser", // Default role for public registration
                FirstName = request.FirstName,
                LastName = request.LastName,
                PhoneNumber = request.PhoneNumber,
                Department = request.Department,
                Status = "Pending",
                IsActive = false, // Inactive until approved
                CreatedAt = DateTime.UtcNow,
                Profile = JsonDocument.Parse("{}")
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            // Log audit
            await _auditService.LogAsync("USER_REGISTERED", user.Id, new { 
                Email = user.Email,
                Role = user.Role,
                Status = user.Status
            }, ipAddress);

            // Send notification to admin users
            await NotifyAdminsOfPendingRegistration(user.Email);

            return new RegistrationResult(true, "Registration submitted successfully. Your account is pending approval.", MapToUserDto(user));
        }

        public async Task<bool> ValidateEmailAvailabilityAsync(string email)
        {
            var existingUser = await _context.Users
                .FirstOrDefaultAsync(u => u.Email == email);

            return existingUser == null;
        }

        private static bool IsPasswordComplex(string password)
        {
            if (string.IsNullOrEmpty(password) || password.Length < 8)
                return false;

            bool hasUpper = password.Any(char.IsUpper);
            bool hasLower = password.Any(char.IsLower);
            bool hasDigit = password.Any(char.IsDigit);
            bool hasSpecial = password.Any(c => !char.IsLetterOrDigit(c));

            return hasUpper && hasLower && hasDigit && hasSpecial;
        }

        private async Task NotifyAdminsOfPendingRegistration(string pendingUserEmail)
        {
            var adminUsers = await _context.Users
                .Where(u => u.Role == "Admin" && u.IsActive)
                .ToListAsync();

            foreach (var admin in adminUsers)
            {
                try
                {
                    await _emailService.SendPendingApprovalNotificationAsync(admin.Email, pendingUserEmail);
                }
                catch
                {
                    // Log error but continue with other admins
                    // In production, you'd want proper error logging here
                }
            }
        }

        private static UserDto MapToUserDto(User user)
        {
            return new UserDto(
                user.Id,
                user.Email,
                user.FirstName ?? string.Empty,
                user.LastName ?? string.Empty,
                user.Role,
                user.Status ?? "Pending",
                user.CreatedAt,
                user.LastLoginAt,
                user.PhoneNumber,
                user.Department,
                user.ApprovedAt,
                user.DeactivatedAt
            );
        }
    }
}
