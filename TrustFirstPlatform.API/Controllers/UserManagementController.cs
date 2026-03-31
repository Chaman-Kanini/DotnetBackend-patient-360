using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using TrustFirstPlatform.Application.DTOs;
using TrustFirstPlatform.Application.Services;

namespace TrustFirstPlatform.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = "Admin")]
    public class UserManagementController : ControllerBase
    {
        private readonly IUserManagementService _userManagementService;

        public UserManagementController(IUserManagementService userManagementService)
        {
            _userManagementService = userManagementService;
        }

        [HttpPost("create")]
        public async Task<ActionResult<UserDto>> CreateUser([FromBody] CreateUserRequest request)
        {
            try
            {
                var adminId = GetCurrentUserId();
                var ipAddress = GetClientIpAddress();
                var user = await _userManagementService.CreateUserAsync(request, adminId, ipAddress);
                return CreatedAtAction(nameof(GetUserById), new { id = user.Id }, user);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(new { message = ex.Message });
            }
            catch (Exception)
            {
                return StatusCode(500, new { message = "An error occurred while creating the user" });
            }
        }

        [HttpGet]
        public async Task<ActionResult<PagedResult<UserDto>>> GetUsers([FromQuery] UserFilterRequest filter)
        {
            try
            {
                var users = await _userManagementService.GetUsersAsync(filter);
                return Ok(users);
            }
            catch (Exception)
            {
                return StatusCode(500, new { message = "An error occurred while retrieving users" });
            }
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<UserDto>> GetUserById(Guid id)
        {
            try
            {
                var user = await _userManagementService.GetUserByIdAsync(id);
                if (user == null)
                {
                    return NotFound(new { message = "User not found" });
                }
                return Ok(user);
            }
            catch (Exception)
            {
                return StatusCode(500, new { message = "An error occurred while retrieving the user" });
            }
        }

        [HttpPut("{id}")]
        public async Task<ActionResult<UserDto>> UpdateUser(Guid id, [FromBody] UpdateUserRequest request)
        {
            try
            {
                var adminId = GetCurrentUserId();
                var ipAddress = GetClientIpAddress();
                var user = await _userManagementService.UpdateUserAsync(id, request, adminId, ipAddress);
                return Ok(user);
            }
            catch (InvalidOperationException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (Exception)
            {
                return StatusCode(500, new { message = "An error occurred while updating the user" });
            }
        }

        [HttpPut("{id}/role")]
        public async Task<ActionResult> UpdateUserRole(Guid id, [FromBody] UpdateRoleRequest request)
        {
            try
            {
                var adminId = GetCurrentUserId();
                var ipAddress = GetClientIpAddress();
                await _userManagementService.UpdateUserRoleAsync(id, request.Role, adminId, ipAddress);
                return Ok(new { message = "User role updated successfully" });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception)
            {
                return StatusCode(500, new { message = "An error occurred while updating the user role" });
            }
        }

        [HttpPost("{id}/deactivate")]
        public async Task<ActionResult> DeactivateUser(Guid id, [FromBody] DeactivateUserRequest request)
        {
            try
            {
                var adminId = GetCurrentUserId();
                var ipAddress = GetClientIpAddress();
                await _userManagementService.DeactivateUserAsync(id, request.Reason, adminId, ipAddress);
                return Ok(new { message = "User deactivated successfully" });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception)
            {
                return StatusCode(500, new { message = "An error occurred while deactivating the user" });
            }
        }

        [HttpPost("{id}/reactivate")]
        public async Task<ActionResult> ReactivateUser(Guid id)
        {
            try
            {
                var adminId = GetCurrentUserId();
                var ipAddress = GetClientIpAddress();
                await _userManagementService.ReactivateUserAsync(id, adminId, ipAddress);
                return Ok(new { message = "User reactivated successfully" });
            }
            catch (InvalidOperationException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (Exception)
            {
                return StatusCode(500, new { message = "An error occurred while reactivating the user" });
            }
        }

        [HttpPost("{id}/approve")]
        public async Task<ActionResult<UserDto>> ApproveUser(Guid id)
        {
            try
            {
                var adminId = GetCurrentUserId();
                var ipAddress = GetClientIpAddress();
                var user = await _userManagementService.ApproveUserAsync(id, adminId, ipAddress);
                return Ok(user);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception)
            {
                return StatusCode(500, new { message = "An error occurred while approving the user" });
            }
        }

        [HttpPost("{id}/reject")]
        public async Task<ActionResult> RejectUser(Guid id, [FromBody] RejectUserRequest request)
        {
            try
            {
                var adminId = GetCurrentUserId();
                var ipAddress = GetClientIpAddress();
                await _userManagementService.RejectUserAsync(id, request.Reason, adminId, ipAddress);
                return Ok(new { message = "User rejected successfully" });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception)
            {
                return StatusCode(500, new { message = "An error occurred while rejecting the user" });
            }
        }

        [HttpGet("pending")]
        public async Task<ActionResult<UserDto[]>> GetPendingUsers()
        {
            try
            {
                var users = await _userManagementService.GetPendingUsersAsync();
                return Ok(users);
            }
            catch (Exception)
            {
                return StatusCode(500, new { message = "An error occurred while retrieving pending users" });
            }
        }

        private Guid GetCurrentUserId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
            {
                throw new UnauthorizedAccessException("Invalid user identifier");
            }
            return userId;
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

    public record UpdateRoleRequest(string Role);
    public record DeactivateUserRequest(string Reason);
    public record RejectUserRequest(string Reason);
}
