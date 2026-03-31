using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using TrustFirstPlatform.Infrastructure.Data;
using TrustFirstPlatform.Domain.Entities;

namespace TrustFirstPlatform.API.Middleware
{
    public class SessionActivityMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<SessionActivityMiddleware> _logger;
        private readonly IServiceProvider _serviceProvider;

        public SessionActivityMiddleware(RequestDelegate next, ILogger<SessionActivityMiddleware> logger, IServiceProvider serviceProvider)
        {
            _next = next;
            _logger = logger;
            _serviceProvider = serviceProvider;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            // Skip session tracking for non-authenticated requests
            if (!context.User.Identity?.IsAuthenticated ?? true)
            {
                await _next(context);
                return;
            }

            // Skip session tracking for login, logout, and token refresh endpoints
            var path = context.Request.Path.Value?.ToLower() ?? string.Empty;
            if (path.Contains("/login") || path.Contains("/logout") || path.Contains("/refresh"))
            {
                await _next(context);
                return;
            }

            try
            {
                // Extract token information
                var token = GetTokenFromRequest(context);
                if (token != null)
                {
                    var tokenJti = GetTokenJti(token);
                    if (!string.IsNullOrEmpty(tokenJti))
                    {
                        await UpdateSessionActivityAsync(tokenJti, GetClientIpAddress(context));
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to update session activity");
                // Continue with the request even if session tracking fails
            }

            await _next(context);
        }

        private string? GetTokenFromRequest(HttpContext context)
        {
            var authorizationHeader = context.Request.Headers.Authorization.FirstOrDefault();
            if (authorizationHeader != null && authorizationHeader.StartsWith("Bearer "))
            {
                return authorizationHeader.Substring("Bearer ".Length).Trim();
            }
            return null;
        }

        private string? GetTokenJti(string token)
        {
            try
            {
                var tokenHandler = new JwtSecurityTokenHandler();
                var jsonToken = tokenHandler.ReadToken(token) as JwtSecurityToken;
                return jsonToken?.Id;
            }
            catch
            {
                return null;
            }
        }

        private string GetClientIpAddress(HttpContext context)
        {
            var xForwardedFor = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
            if (!string.IsNullOrEmpty(xForwardedFor))
            {
                return xForwardedFor.Split(',')[0].Trim();
            }

            var xRealIp = context.Request.Headers["X-Real-IP"].FirstOrDefault();
            if (!string.IsNullOrEmpty(xRealIp))
            {
                return xRealIp;
            }

            return context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        }

        private async Task UpdateSessionActivityAsync(string tokenJti, string ipAddress)
        {
            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var session = await dbContext.UserSessions
                .FirstOrDefaultAsync(s => s.TokenJti == tokenJti && !s.IsRevoked && s.ExpiresAt > DateTime.UtcNow);

            if (session != null)
            {
                session.LastActivityAt = DateTime.UtcNow;
                await dbContext.SaveChangesAsync();
                
                _logger.LogDebug("Updated activity for session {SessionId} (Token: {TokenJti})", session.Id, tokenJti);
            }
        }
    }
}
