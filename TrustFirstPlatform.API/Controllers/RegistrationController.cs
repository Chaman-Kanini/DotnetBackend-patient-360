using Microsoft.AspNetCore.Mvc;
using TrustFirstPlatform.Application.DTOs;
using TrustFirstPlatform.Application.Services;

namespace TrustFirstPlatform.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class RegistrationController : ControllerBase
    {
        private readonly IRegistrationService _registrationService;

        public RegistrationController(IRegistrationService registrationService)
        {
            _registrationService = registrationService;
        }

        [HttpPost("register")]
        public async Task<ActionResult<RegistrationResult>> Register([FromBody] PublicRegistrationRequest request)
        {
            try
            {
                var ipAddress = GetClientIpAddress();
                var result = await _registrationService.RegisterAsync(request, ipAddress);
                
                if (result.Success)
                {
                    return Ok(result);
                }
                else
                {
                    return BadRequest(new { message = result.Message });
                }
            }
            catch (Exception)
            {
                return StatusCode(500, new { message = "An error occurred during registration" });
            }
        }

        [HttpGet("check-email")]
        public async Task<ActionResult<bool>> CheckEmailAvailability([FromQuery] string email)
        {
            try
            {
                if (string.IsNullOrEmpty(email))
                {
                    return BadRequest(new { message = "Email is required" });
                }

                var isAvailable = await _registrationService.ValidateEmailAvailabilityAsync(email);
                return Ok(new { available = isAvailable });
            }
            catch (Exception)
            {
                return StatusCode(500, new { message = "An error occurred while checking email availability" });
            }
        }

        private string GetClientIpAddress()
        {
            var xForwardedFor = HttpContext.Request.Headers["X-Forwarded-For"].FirstOrDefault();
            if (!string.IsNullOrEmpty(xForwardedFor))
            {
                return xForwardedFor.Split(',')[0].Trim();
            }

            var xRealIp = HttpContext.Request.Headers["X-Real-IP"].FirstOrDefault();
            if (!string.IsNullOrEmpty(xRealIp))
            {
                return xRealIp;
            }

            return HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        }
    }
}
