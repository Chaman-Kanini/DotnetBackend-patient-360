using Microsoft.Extensions.Options;
using System.Collections.Concurrent;
using System.Net;
using System.Text.Json;

namespace TrustFirstPlatform.API.Middleware
{
    public class RateLimitingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<RateLimitingMiddleware> _logger;
        private readonly RateLimitOptions _options;
        private readonly ConcurrentDictionary<string, RateLimitCounter> _loginCounters = new();
        private readonly ConcurrentDictionary<string, RateLimitCounter> _passwordResetCounters = new();
        private readonly ConcurrentDictionary<string, RateLimitCounter> _registrationCounters = new();

        public RateLimitingMiddleware(RequestDelegate next, ILogger<RateLimitingMiddleware> logger, IOptions<RateLimitOptions> options)
        {
            _next = next;
            _logger = logger;
            _options = options.Value;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var path = context.Request.Path.Value?.ToLowerInvariant() ?? string.Empty;

            if (IsRateLimitedEndpoint(path))
            {
                var clientIdentifier = await GetClientIdentifierAsync(context, path);

                if (path.Contains("/login"))
                {
                    if (!await CheckRateLimitAsync(_loginCounters, clientIdentifier, _options.LoginAttemptsPerMinute, TimeSpan.FromMinutes(1)))
                    {
                        await WriteRateLimitResponse(context, "Too many login attempts. Please try again later.", clientIdentifier);
                        return;
                    }
                }
                else if (path.Contains("/forgot-password"))
                {
                    if (!await CheckRateLimitAsync(_passwordResetCounters, clientIdentifier, _options.PasswordResetAttemptsPerHour, TimeSpan.FromHours(1)))
                    {
                        await WriteRateLimitResponse(context, "Too many password reset requests. Please try again later.", clientIdentifier);
                        return;
                    }
                }
                else if (path.Contains("/register"))
                {
                    if (!await CheckRateLimitAsync(_registrationCounters, clientIdentifier, _options.RegistrationAttemptsPerHour, TimeSpan.FromHours(1)))
                    {
                        await WriteRateLimitResponse(context, "Too many registration attempts. Please try again later.", clientIdentifier);
                        return;
                    }
                }
            }

            await _next(context);
        }

        private static bool IsRateLimitedEndpoint(string path)
        {
            return path.Contains("/api/auth/login") || path.Contains("/api/auth/forgot-password") || path.Contains("/api/auth/register");
        }

        private async Task<string> GetClientIdentifierAsync(HttpContext context, string path)
        {
            if (path.Contains("/forgot-password"))
            {
                // For password reset, try to rate limit by email from request body
                var email = await ExtractEmailFromRequestAsync(context);
                if (!string.IsNullOrEmpty(email))
                {
                    return $"email:{email.ToLowerInvariant()}";
                }
                
                // Fallback to IP if email extraction fails
                _logger.LogWarning("Could not extract email from password reset request, falling back to IP-based rate limiting");
                return $"ip:{GetClientIp(context)}";
            }
            
            // For login and register, rate limit by IP address
            return $"ip:{GetClientIp(context)}";
        }

        private async Task<string?> ExtractEmailFromRequestAsync(HttpContext context)
        {
            try
            {
                // Enable request body buffering
                context.Request.EnableBuffering();
                
                // Read the request body
                using var reader = new StreamReader(context.Request.Body, leaveOpen: true);
                var body = await reader.ReadToEndAsync();
                
                // Reset the stream position for the controller to read
                context.Request.Body.Position = 0;
                
                // Parse JSON to extract email
                using var document = JsonDocument.Parse(body);
                if (document.RootElement.TryGetProperty("email", out var emailProperty))
                {
                    return emailProperty.GetString();
                }
                
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error extracting email from password reset request");
                return null;
            }
        }

        private static string GetClientIp(HttpContext context)
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

        private void CleanupOldCounters(ConcurrentDictionary<string, RateLimitCounter> counters, DateTime now, TimeSpan period)
        {
            var cutoffTime = now - period;
            var keysToRemove = counters
                .Where(kvp => kvp.Value.WindowStart < cutoffTime)
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var key in keysToRemove)
            {
                counters.TryRemove(key, out _);
            }
        }

        private Task<bool> CheckRateLimitAsync(ConcurrentDictionary<string, RateLimitCounter> counters, string clientId, int limit, TimeSpan period)
        {
            var now = DateTime.UtcNow;
            var counter = counters.AddOrUpdate(
                clientId,
                new RateLimitCounter { Count = 1, WindowStart = now },
                (_, existing) =>
                {
                    if (now - existing.WindowStart >= period)
                    {
                        return new RateLimitCounter { Count = 1, WindowStart = now };
                    }

                    return new RateLimitCounter { Count = existing.Count + 1, WindowStart = existing.WindowStart };
                });

            if (now.Ticks % 100 == 0)
            {
                CleanupOldCounters(counters, now, period);
            }

            return Task.FromResult(counter.Count <= limit);
        }

        private async Task WriteRateLimitResponse(HttpContext context, string message, string clientIdentifier)
        {
            context.Response.StatusCode = (int)HttpStatusCode.TooManyRequests;
            context.Response.ContentType = "application/json";

            var response = new
            {
                error = "Rate limit exceeded",
                message,
                retryAfter = "3600" // seconds (1 hour for password reset)
            };

            await context.Response.WriteAsJsonAsync(response);
            _logger.LogWarning("Rate limit exceeded for client {ClientIdentifier}", clientIdentifier);
        }
    }

    public class RateLimitOptions
    {
        public int LoginAttemptsPerMinute { get; set; } = 5;
        public int PasswordResetAttemptsPerHour { get; set; } = 3;
        public int RegistrationAttemptsPerHour { get; set; } = 3;
    }

    public class RateLimitCounter
    {
        public int Count { get; set; }
        public DateTime WindowStart { get; set; }
    }
}
