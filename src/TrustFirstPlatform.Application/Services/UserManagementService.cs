using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using TrustFirstPlatform.Application.DTOs;
using TrustFirstPlatform.Domain.Entities;
using TrustFirstPlatform.Infrastructure.Data;
using TrustFirstPlatform.Infrastructure.Email;

namespace TrustFirstPlatform.Application.Services
{
    public class UserManagementService : IUserManagementService
    {
        private readonly AppDbContext _context;
        private readonly IAuditService _auditService;
        private readonly IEmailService _emailService;
        private readonly ILogger<UserManagementService> _logger;

        public UserManagementService(
            AppDbContext context,
            IAuditService auditService,
            IEmailService emailService,
            ILogger<UserManagementService> logger)
        {
            _context = context;
            _auditService = auditService;
            _emailService = emailService;
            _logger = logger;
        }

        public async Task<UserDto> CreateUserAsync(CreateUserRequest request, Guid adminId, string ipAddress)
        {
            // Validate role
            if (request.Role != "Admin" && request.Role != "StandardUser")
            {
                throw new ArgumentException("Invalid role. Must be 'Admin' or 'StandardUser'");
            }

            // Check if email already exists
            var existingUser = await _context.Users.FirstOrDefaultAsync(u => u.Email == request.Email);
            if (existingUser != null)
            {
                throw new InvalidOperationException("Email already registered");
            }

            // Generate temporary password
            var temporaryPassword = GenerateTemporaryPassword();

            // Create new user
            var user = new User
            {
                Id = Guid.NewGuid(),
                Email = request.Email,
                PasswordHash = PasswordHasher.HashPassword(temporaryPassword),
                Role = request.Role,
                FirstName = request.FirstName,
                LastName = request.LastName,
                PhoneNumber = request.PhoneNumber,
                Department = request.Department,
                Status = "Active",
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                Profile = JsonDocument.Parse("{}"),
                ApprovedAt = DateTime.UtcNow,
                ApprovedBy = adminId
            };

            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                _context.Users.Add(user);
                await _context.SaveChangesAsync();

                // Send welcome email
                await _emailService.SendWelcomeEmailAsync(user.Email, user.FirstName!, temporaryPassword);

                // Log audit
                await _auditService.LogAsync("USER_CREATED", adminId, new {
                    UserId = user.Id,
                    Email = user.Email,
                    Role = user.Role
                }, ipAddress);

                await transaction.CommitAsync();
                return MapToUserDto(user);
            }
            catch (DbUpdateException ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Failed to persist user {Email}. Transaction rolled back.", request.Email);
                throw new InvalidOperationException("Unable to create user due to a persistence error. Please contact support.");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Failed to create user {Email}. Transaction rolled back.", request.Email);
                throw new InvalidOperationException("Unable to send welcome email. User was not created. Please verify email settings and try again.");
            }
        }

        public async Task<UserDto> UpdateUserAsync(Guid userId, UpdateUserRequest request, Guid adminId, string ipAddress)
        {
            var user = await _context.Users.FindAsync(userId);
            if (user == null)
            {
                throw new InvalidOperationException("User not found");
            }

            // Update fields if provided
            if (request.FirstName != null) user.FirstName = request.FirstName;
            if (request.LastName != null) user.LastName = request.LastName;
            if (request.PhoneNumber != null) user.PhoneNumber = request.PhoneNumber;
            if (request.Department != null) user.Department = request.Department;

            await _context.SaveChangesAsync();

            await _auditService.LogAsync("USER_UPDATED", adminId, new { 
                UserId = user.Id, 
                Email = user.Email 
            }, ipAddress);

            return MapToUserDto(user);
        }

        public async Task<bool> DeactivateUserAsync(Guid userId, string reason, Guid adminId, string ipAddress)
        {
            var user = await _context.Users.FindAsync(userId);
            if (user == null)
            {
                throw new InvalidOperationException("User not found");
            }

            // Edge case: Prevent admin self-deactivation
            if (userId == adminId)
            {
                throw new InvalidOperationException("Cannot deactivate your own account");
            }

            // Edge case: Prevent deactivation of last admin
            if (user.Role == "Admin")
            {
                var adminCount = await _context.Users.CountAsync(u => u.Role == "Admin" && u.IsActive);
                if (adminCount <= 1)
                {
                    throw new InvalidOperationException("Cannot deactivate the last admin account");
                }
            }

            user.IsActive = false;
            user.Status = "Inactive";
            user.DeactivatedAt = DateTime.UtcNow;
            user.DeactivatedBy = adminId;
            user.DeactivationReason = reason;

            await _context.SaveChangesAsync();

            // Invalidate all sessions for the deactivated user
            await InvalidateUserSessions(userId);

            await _auditService.LogAsync("USER_DEACTIVATED", adminId, new { 
                UserId = user.Id, 
                Email = user.Email, 
                Reason = reason 
            }, ipAddress);

            // Send deactivation email
            try
            {
                await _emailService.SendAccountDeactivatedEmailAsync(user.Email, user.FirstName!, reason);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send deactivation email to {Email}", user.Email);
            }

            return true;
        }

        public async Task<bool> ReactivateUserAsync(Guid userId, Guid adminId, string ipAddress)
        {
            var user = await _context.Users.FindAsync(userId);
            if (user == null)
            {
                throw new InvalidOperationException("User not found");
            }

            user.IsActive = true;
            user.Status = "Active";
            user.DeactivatedAt = null;
            user.DeactivatedBy = null;
            user.DeactivationReason = null;

            await _context.SaveChangesAsync();

            await _auditService.LogAsync("USER_REACTIVATED", adminId, new { 
                UserId = user.Id, 
                Email = user.Email 
            }, ipAddress);

            return true;
        }

        public async Task<UserDto> ApproveUserAsync(Guid userId, Guid adminId, string ipAddress)
        {
            var user = await _context.Users.FindAsync(userId);
            if (user == null)
            {
                throw new InvalidOperationException("User not found");
            }

            if (user.Status != "Pending")
            {
                throw new InvalidOperationException("User is not pending approval");
            }

            user.Status = "Active";
            user.IsActive = true;
            user.ApprovedAt = DateTime.UtcNow;
            user.ApprovedBy = adminId;

            await _context.SaveChangesAsync();

            await _auditService.LogAsync("USER_APPROVED", adminId, new { 
                UserId = user.Id, 
                Email = user.Email 
            }, ipAddress);

            // Send approval email
            try
            {
                await _emailService.SendAccountApprovedEmailAsync(user.Email, user.FirstName!);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send approval email to {Email}", user.Email);
            }

            return MapToUserDto(user);
        }

        public async Task<bool> RejectUserAsync(Guid userId, string reason, Guid adminId, string ipAddress)
        {
            var user = await _context.Users.FindAsync(userId);
            if (user == null)
            {
                throw new InvalidOperationException("User not found");
            }

            if (user.Status != "Pending")
            {
                throw new InvalidOperationException("User is not pending approval");
            }

            user.Status = "Rejected";
            user.IsActive = false;
            user.DeactivatedAt = DateTime.UtcNow;
            user.DeactivatedBy = adminId;
            user.DeactivationReason = reason;

            await _context.SaveChangesAsync();

            await _auditService.LogAsync("USER_REJECTED", adminId, new { 
                UserId = user.Id, 
                Email = user.Email, 
                Reason = reason 
            }, ipAddress);

            // Send rejection email
            try
            {
                await _emailService.SendAccountRejectedEmailAsync(user.Email, user.FirstName!, reason);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send rejection email to {Email}", user.Email);
            }

            return true;
        }

        public async Task<PagedResult<UserDto>> GetUsersAsync(UserFilterRequest filter)
        {
            var query = _context.Users.AsQueryable();

            // Apply filters
            if (!string.IsNullOrEmpty(filter.SearchTerm))
            {
                query = query.Where(u => 
                    u.Email.Contains(filter.SearchTerm) ||
                    (u.FirstName != null && u.FirstName.Contains(filter.SearchTerm)) ||
                    (u.LastName != null && u.LastName.Contains(filter.SearchTerm)));
            }

            if (!string.IsNullOrEmpty(filter.Role))
            {
                query = query.Where(u => u.Role == filter.Role);
            }

            if (!string.IsNullOrEmpty(filter.Status))
            {
                query = query.Where(u => u.Status == filter.Status);
            }

            var totalCount = await query.CountAsync();

            var users = await query
                .OrderBy(u => u.CreatedAt)
                .Skip((filter.Page - 1) * filter.PageSize)
                .Take(filter.PageSize)
                .Select(u => MapToUserDto(u))
                .ToArrayAsync();

            var totalPages = (int)Math.Ceiling((double)totalCount / filter.PageSize);

            return new PagedResult<UserDto>(
                users,
                totalCount,
                filter.Page,
                filter.PageSize,
                totalPages,
                filter.Page < totalPages,
                filter.Page > 1
            );
        }

        public async Task<UserDto?> GetUserByIdAsync(Guid userId)
        {
            var user = await _context.Users.FindAsync(userId);
            return user != null ? MapToUserDto(user) : null;
        }

        public async Task<bool> UpdateUserRoleAsync(Guid userId, string newRole, Guid adminId, string ipAddress)
        {
            var user = await _context.Users.FindAsync(userId);
            if (user == null)
            {
                throw new InvalidOperationException("User not found");
            }

            if (newRole != "Admin" && newRole != "StandardUser")
            {
                throw new ArgumentException("Invalid role. Must be 'Admin' or 'StandardUser'");
            }

            // Edge case: Prevent removing admin role from last admin
            if (user.Role == "Admin" && newRole != "Admin")
            {
                var adminCount = await _context.Users.CountAsync(u => u.Role == "Admin" && u.IsActive);
                if (adminCount <= 1)
                {
                    throw new InvalidOperationException("Cannot remove admin role from the last admin account");
                }
            }

            var oldRole = user.Role;
            user.Role = newRole;

            await _context.SaveChangesAsync();

            await _auditService.LogAsync("USER_ROLE_CHANGED", adminId, new { 
                UserId = user.Id, 
                Email = user.Email, 
                OldRole = oldRole, 
                NewRole = newRole 
            }, ipAddress);

            return true;
        }

        public async Task<UserDto[]> GetPendingUsersAsync()
        {
            var pendingUsers = await _context.Users
                .Where(u => u.Status == "Pending")
                .OrderBy(u => u.CreatedAt)
                .Select(u => MapToUserDto(u))
                .ToArrayAsync();

            return pendingUsers;
        }

        private async Task InvalidateUserSessions(Guid userId)
        {
            var sessions = await _context.UserSessions
                .Where(s => s.UserId == userId && !s.IsRevoked && s.ExpiresAt > DateTime.UtcNow)
                .ToListAsync();

            foreach (var session in sessions)
            {
                session.IsRevoked = true;
            }

            await _context.SaveChangesAsync();
        }

        private static UserDto MapToUserDto(User user)
        {
            return new UserDto(
                user.Id,
                user.Email,
                user.FirstName ?? string.Empty,
                user.LastName ?? string.Empty,
                user.Role,
                user.Status ?? "Active",
                user.CreatedAt,
                user.LastLoginAt,
                user.PhoneNumber,
                user.Department,
                user.ApprovedAt,
                user.DeactivatedAt
            );
        }

        private static string GenerateTemporaryPassword()
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789!@#$%";
            var random = new Random();
            var password = new char[12];

            for (int i = 0; i < password.Length; i++)
            {
                password[i] = chars[random.Next(chars.Length)];
            }

            return new string(password);
        }
    }
}
