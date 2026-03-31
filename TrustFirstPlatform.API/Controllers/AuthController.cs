using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;
using TrustFirstPlatform.Application.DTOs;
using TrustFirstPlatform.Application.Services;

namespace TrustFirstPlatform.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;
        private readonly IPasswordResetService _passwordResetService;
        private readonly JwtTokenService _jwtTokenService;
        private readonly IAuditService _auditService;

        public AuthController(
            IAuthService authService,
            IPasswordResetService passwordResetService,
            JwtTokenService jwtTokenService,
            IAuditService auditService)
        {
            _authService = authService;
            _passwordResetService = passwordResetService;
            _jwtTokenService = jwtTokenService;
            _auditService = auditService;
        }

        [HttpPost("login")]
        public async Task<ActionResult<LoginResponse>> Login([FromBody] LoginRequest request)
        {
            try
            {
                var ipAddress = GetClientIpAddress();
                var response = await _authService.LoginAsync(request, ipAddress);
                return Ok(response);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new { message = ex.Message });
            }
            catch (Exception)
            {
                return StatusCode(500, new { message = "An error occurred during login" });
            }
        }

        [HttpPost("logout")]
        [Authorize]
        public async Task<IActionResult> Logout()
        {
            try
            {
                var userId = GetCurrentUserId();
                var tokenJti = GetCurrentTokenJti();
                
                if (userId == null || string.IsNullOrEmpty(tokenJti))
                {
                    return BadRequest(new { message = "Invalid token" });
                }

                var result = await _authService.LogoutAsync(userId.Value, tokenJti);
                
                if (result)
                {
                    return Ok(new { message = "Logout successful" });
                }
                
                return BadRequest(new { message = "Session not found" });
            }
            catch (Exception)
            {
                return StatusCode(500, new { message = "An error occurred during logout" });
            }
        }

        [HttpPost("forgot-password")]
        public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request)
        {
            try
            {
                var result = await _passwordResetService.RequestResetAsync(request);
                
                if (result)
                {
                    return Ok(new { message = "Password reset link sent to your email if account exists" });
                }
                
                return BadRequest(new { message = "Failed to process password reset request" });
            }
            catch (Exception)
            {
                return StatusCode(500, new { message = "An error occurred while processing password reset request" });
            }
        }

        [HttpPost("reset-password")]
        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request)
        {
            try
            {
                var result = await _passwordResetService.ResetPasswordAsync(request);
                
                if (result)
                {
                    return Ok(new { message = "Password reset successful" });
                }
                
                return BadRequest(new { message = "Invalid or expired reset token" });
            }
            catch (Exception)
            {
                return StatusCode(500, new { message = "An error occurred while resetting password" });
            }
        }

        [HttpGet("validate-token")]
        public async Task<IActionResult> ValidateResetToken([FromQuery] string token)
        {
            try
            {
                var result = await _passwordResetService.ValidateTokenAsync(token);
                
                if (result)
                {
                    return Ok(new { valid = true });
                }
                
                return BadRequest(new { valid = false, message = "Invalid or expired token" });
            }
            catch (Exception)
            {
                return StatusCode(500, new { message = "An error occurred while validating token" });
            }
        }

        [HttpGet("me")]
        [Authorize]
        public async Task<IActionResult> GetCurrentUser()
        {
            try
            {
                var userId = GetCurrentUserId();
                var tokenJti = GetCurrentTokenJti();
                
                if (userId == null || string.IsNullOrEmpty(tokenJti))
                {
                    return BadRequest(new { message = "Invalid token" });
                }

                var isValid = await _authService.ValidateSessionAsync(userId.Value, tokenJti);
                
                if (!isValid)
                {
                    return Unauthorized(new { message = "Session expired or invalid" });
                }

                var email = User.FindFirst(ClaimTypes.Email)?.Value;
                var role = User.FindFirst(ClaimTypes.Role)?.Value;

                return Ok(new
                {
                    Id = userId,
                    Email = email,
                    Role = role
                });
            }
            catch (Exception)
            {
                return StatusCode(500, new { message = "An error occurred while fetching user data" });
            }
        }

        private Guid? GetCurrentUserId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return Guid.TryParse(userIdClaim, out var userId) ? userId : null;
        }

        private string GetCurrentTokenJti()
        {
            return User.FindFirst(JwtRegisteredClaimNames.Jti)?.Value ?? string.Empty;
        }

        private string GetClientIpAddress()
        {
            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
            
            // Check for X-Forwarded-For header (when behind proxy)
            if (HttpContext.Request.Headers.TryGetValue("X-Forwarded-For", out var forwardedFor))
            {
                ipAddress = forwardedFor.FirstOrDefault()?.Split(',')[0].Trim();
            }
            
            return ipAddress ?? "Unknown";
        }
    }
}
