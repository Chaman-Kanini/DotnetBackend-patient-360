using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography;
using System.Text;
using TrustFirstPlatform.Application.DTOs;
using TrustFirstPlatform.Domain.Entities;
using TrustFirstPlatform.Infrastructure.Data;
using TrustFirstPlatform.Infrastructure.Email;

namespace TrustFirstPlatform.Application.Services
{
    public class PasswordResetService : IPasswordResetService
    {
        private readonly AppDbContext _context;
        private readonly IAuditService _auditService;
        private readonly IAuthService _authService;
        private readonly IEmailService _emailService;
        private readonly IConfiguration _configuration;
        private readonly ILogger<PasswordResetService> _logger;

        public PasswordResetService(AppDbContext context, IAuditService auditService, IAuthService authService, IEmailService emailService, IConfiguration configuration, ILogger<PasswordResetService> logger)
        {
            _context = context;
            _auditService = auditService;
            _authService = authService;
            _emailService = emailService;
            _configuration = configuration;
            _logger = logger;
        }

        public async Task<bool> RequestResetAsync(ForgotPasswordRequest request)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == request.Email);
            
            if (user == null)
            {
                // Don't reveal that the user doesn't exist
                return true;
            }

            // Invalidate any existing reset tokens
            var existingTokens = await _context.PasswordResetTokens
                .Where(t => t.UserId == user.Id && !t.IsUsed && t.ExpiresAt > DateTime.UtcNow)
                .ToListAsync();

            foreach (var token in existingTokens)
            {
                token.IsUsed = true;
            }

            // Generate new reset token
            var resetToken = GenerateSecureToken();
            var passwordResetToken = new PasswordResetToken
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                Token = resetToken,
                ExpiresAt = DateTime.UtcNow.AddHours(1), // 1-hour expiration
                IsUsed = false
            };

            _context.PasswordResetTokens.Add(passwordResetToken);
            await _context.SaveChangesAsync();

            await _auditService.LogAsync("PASSWORD_RESET_REQUESTED", user.Id, new { TokenId = passwordResetToken.Id }, "System");

            await SendResetEmailAsync(user.Email, resetToken);
            
            return true;
        }

        public async Task<bool> ValidateTokenAsync(string token)
        {
            var resetToken = await _context.PasswordResetTokens
                .FirstOrDefaultAsync(t => t.Token == token && !t.IsUsed && t.ExpiresAt > DateTime.UtcNow);

            return resetToken != null;
        }

        public async Task<bool> ResetPasswordAsync(ResetPasswordRequest request)
        {
            var resetToken = await _context.PasswordResetTokens
                .FirstOrDefaultAsync(t => t.Token == request.Token && !t.IsUsed && t.ExpiresAt > DateTime.UtcNow);

            if (resetToken == null)
            {
                return false;
            }

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == resetToken.UserId);
            if (user == null)
            {
                return false;
            }

            // Update password
            user.PasswordHash = PasswordHasher.HashPassword(request.NewPassword);
            user.FailedLoginAttempts = 0; // Reset failed attempts
            user.LockoutEnd = null; // Remove any lockout

            // Mark token as used
            resetToken.IsUsed = true;

            // Invalidate all existing sessions for security
            await _authService.InvalidateAllSessionsAsync(user.Id);

            await _context.SaveChangesAsync();

            await _auditService.LogAsync("PASSWORD_RESET_COMPLETED", user.Id, new { TokenId = resetToken.Id }, "System");

            return true;
        }

        private string GenerateSecureToken()
        {
            using var rng = RandomNumberGenerator.Create();
            var tokenBytes = new byte[32];
            rng.GetBytes(tokenBytes);
            return Convert.ToBase64String(tokenBytes).Replace("/", "_").Replace("+", "-");
        }

        private async Task SendResetEmailAsync(string toEmail, string resetToken)
        {
            try
            {
                var frontendBaseUrl = _configuration["App:FrontendBaseUrl"] ?? "http://localhost:3000";
                await _emailService.SendPasswordResetEmailAsync(toEmail, resetToken, frontendBaseUrl);
                _logger.LogInformation("Password reset email queued/sent for {Email}", toEmail);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send password reset email for {Email}", toEmail);
                // Log the error but don't fail the password reset request
                // In development, we can log the token for testing
                var env = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
                if (string.Equals(env, "Development", StringComparison.OrdinalIgnoreCase))
                {
                    Console.WriteLine($"Password reset token for {toEmail}: {resetToken}");
                }
                
                // In a production environment, you might want to use a fallback email service
                // or queue the email for retry
            }
        }
    }
}
