using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using TrustFirstPlatform.Application.DTOs;
using TrustFirstPlatform.Domain.Entities;
using TrustFirstPlatform.Infrastructure.Data;

namespace TrustFirstPlatform.Application.Services
{
    public class AuthService : IAuthService
    {
        private readonly AppDbContext _context;
        private readonly JwtTokenService _jwtTokenService;
        private readonly IAuditService _auditService;

        public AuthService(AppDbContext context, JwtTokenService jwtTokenService, IAuditService auditService)
        {
            _context = context;
            _jwtTokenService = jwtTokenService;
            _auditService = auditService;
        }

        public async Task<LoginResponse> LoginAsync(LoginRequest request, string ipAddress)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == request.Email);
            
            if (user == null)
            {
                await _auditService.LogAsync("LOGIN_FAILED", null, new { Email = request.Email, Reason = "User not found" }, ipAddress);
                throw new UnauthorizedAccessException("Invalid credentials");
            }

            if (!user.IsActive)
            {
                await _auditService.LogAsync("LOGIN_FAILED", user.Id, new { Reason = "Account inactive" }, ipAddress);
                throw new UnauthorizedAccessException("Account is inactive");
            }

            if (user.LockoutEnd.HasValue && user.LockoutEnd > DateTime.UtcNow)
            {
                await _auditService.LogAsync("LOGIN_FAILED", user.Id, new { Reason = "Account locked", LockoutEnd = user.LockoutEnd }, ipAddress);
                throw new UnauthorizedAccessException($"Account is locked until {user.LockoutEnd:yyyy-MM-dd HH:mm:ss}");
            }

            if (!PasswordHasher.VerifyPassword(request.Password, user.PasswordHash))
            {
                user.FailedLoginAttempts++;
                
                if (user.FailedLoginAttempts >= 5)
                {
                    user.LockoutEnd = DateTime.UtcNow.AddMinutes(30);
                    await _context.SaveChangesAsync();
                    await _auditService.LogAsync("ACCOUNT_LOCKED", user.Id, new { FailedAttempts = user.FailedLoginAttempts }, ipAddress);
                    throw new UnauthorizedAccessException("Account has been locked due to multiple failed login attempts");
                }

                await _context.SaveChangesAsync();
                await _auditService.LogAsync("LOGIN_FAILED", user.Id, new { FailedAttempts = user.FailedLoginAttempts }, ipAddress);
                throw new UnauthorizedAccessException("Invalid credentials");
            }

            // Reset failed attempts on successful login
            user.FailedLoginAttempts = 0;
            user.LockoutEnd = null;
            user.LastLoginAt = DateTime.UtcNow;

            // Check for existing sessions and invalidate them (prevent concurrent sessions)
            var existingSessions = await _context.UserSessions
                .Where(s => s.UserId == user.Id && !s.IsRevoked && s.ExpiresAt > DateTime.UtcNow)
                .ToListAsync();

            foreach (var session in existingSessions)
            {
                session.IsRevoked = true;
            }

            // Generate new token
            var token = _jwtTokenService.GenerateToken(user.Id, user.Email, user.Role);
            var tokenJti = _jwtTokenService.GetTokenJti(token);

            // Create new session
            var userSession = new UserSession
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                TokenJti = tokenJti,
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddMinutes(15), // 15-minute session timeout
                LastActivityAt = DateTime.UtcNow,
                IsRevoked = false,
                IpAddress = ipAddress
            };

            _context.UserSessions.Add(userSession);
            await _context.SaveChangesAsync();

            await _auditService.LogAsync("LOGIN_SUCCESS", user.Id, new { SessionId = userSession.Id }, ipAddress);

            return new LoginResponse
            {
                Token = token,
                ExpiresAt = userSession.ExpiresAt,
                User = new UserDto(
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
                )
            };
        }

        public async Task<bool> LogoutAsync(Guid userId, string tokenJti)
        {
            var session = await _context.UserSessions
                .FirstOrDefaultAsync(s => s.UserId == userId && s.TokenJti == tokenJti && !s.IsRevoked);

            if (session == null)
            {
                return false;
            }

            session.IsRevoked = true;
            await _context.SaveChangesAsync();

            await _auditService.LogAsync("LOGOUT", userId, new { SessionId = session.Id }, session.IpAddress);
            return true;
        }

        public async Task<bool> ValidateSessionAsync(Guid userId, string tokenJti)
        {
            var session = await _context.UserSessions
                .FirstOrDefaultAsync(s => s.UserId == userId && s.TokenJti == tokenJti && !s.IsRevoked && s.ExpiresAt > DateTime.UtcNow);

            if (session == null)
            {
                return false;
            }

            // Update last activity
            session.LastActivityAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return true;
        }

        public async Task InvalidateAllSessionsAsync(Guid userId)
        {
            var sessions = await _context.UserSessions
                .Where(s => s.UserId == userId && !s.IsRevoked)
                .ToListAsync();

            foreach (var session in sessions)
            {
                session.IsRevoked = true;
            }

            await _context.SaveChangesAsync();
        }
    }
}
