using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.IdentityModel.Tokens.Jwt;
using TrustFirstPlatform.Infrastructure.Data;

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
            if (!context.User.Identity?.IsAuthenticated ?? true)
            {
                await _next(context);
                return;
            }

            var path = context.Request.Path.Value?.ToLowerInvariant() ?? string.Empty;
            if (path.Contains("/login") || path.Contains("/logout") || path.Contains("/refresh"))
            {
                await _next(context);
                return;
            }

            try
            {
                var token = GetTokenFromRequest(context);
                if (token != null)
                {
                    var tokenJti = GetTokenJti(token);
                    if (!string.IsNullOrWhiteSpace(tokenJti))
                    {
                        await UpdateSessionActivityAsync(tokenJti);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to update session activity");
            }

            await _next(context);
        }

        private static string? GetTokenFromRequest(HttpContext context)
        {
            var authorizationHeader = context.Request.Headers.Authorization.FirstOrDefault();
            if (authorizationHeader != null && authorizationHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                return authorizationHeader["Bearer ".Length..].Trim();
            }

            return null;
        }

        private static string? GetTokenJti(string token)
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

        private async Task UpdateSessionActivityAsync(string tokenJti)
        {
            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var session = await dbContext.UserSessions
                .FirstOrDefaultAsync(s => s.TokenJti == tokenJti && !s.IsRevoked && s.ExpiresAt > DateTime.UtcNow);

            if (session != null)
            {
                session.LastActivityAt = DateTime.UtcNow;
                await dbContext.SaveChangesAsync();
            }
        }
    }
}
