using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using TrustFirstPlatform.Application.DTOs;
using TrustFirstPlatform.Application.Services;

namespace TrustFirstPlatform.API.Controllers
{
    [ApiController]
    [Route("api/admin")]
    [Authorize(Roles = "Admin")]
    public class AdminDashboardController : ControllerBase
    {
        private readonly IAdminDashboardService _dashboardService;
        private readonly IAuditService _auditService;
        private readonly ILogger<AdminDashboardController> _logger;

        public AdminDashboardController(
            IAdminDashboardService dashboardService,
            IAuditService auditService,
            ILogger<AdminDashboardController> logger)
        {
            _dashboardService = dashboardService;
            _auditService = auditService;
            _logger = logger;
        }

        #region Audit Logs

        [HttpGet("audit-logs")]
        public async Task<ActionResult<PagedResult<AuditLogDto>>> GetAuditLogs(
            [FromQuery] AuditLogFilterRequest filter)
        {
            try
            {
                var adminId = GetCurrentUserId();
                var ipAddress = GetClientIpAddress();

                await _auditService.LogAsync("AUDIT_LOG_VIEWED", adminId, new
                {
                    Filters = new { filter.Action, filter.SearchTerm, filter.StartDate, filter.EndDate }
                }, ipAddress);

                var result = await _dashboardService.GetAuditLogsAsync(filter);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving audit logs");
                return StatusCode(500, new { message = "An error occurred while retrieving audit logs" });
            }
        }

        [HttpGet("audit-logs/export")]
        public async Task<ActionResult<List<AuditLogDto>>> ExportAuditLogs(
            [FromQuery] AuditLogExportRequest filter)
        {
            try
            {
                var adminId = GetCurrentUserId();
                var ipAddress = GetClientIpAddress();

                await _auditService.LogAsync("AUDIT_LOG_EXPORTED", adminId, new
                {
                    Filters = new { filter.Action, filter.SearchTerm, filter.StartDate, filter.EndDate }
                }, ipAddress);

                var result = await _dashboardService.ExportAuditLogsAsync(filter);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting audit logs");
                return StatusCode(500, new { message = "An error occurred while exporting audit logs" });
            }
        }

        [HttpGet("audit-logs/actions")]
        public async Task<ActionResult<List<string>>> GetDistinctActions()
        {
            try
            {
                var result = await _dashboardService.GetDistinctActionsAsync();
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving distinct actions");
                return StatusCode(500, new { message = "An error occurred while retrieving actions" });
            }
        }

        #endregion

        #region Dashboard Summary

        [HttpGet("dashboard/summary")]
        public async Task<ActionResult<DashboardSummaryDto>> GetDashboardSummary()
        {
            try
            {
                var result = await _dashboardService.GetDashboardSummaryAsync();
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving dashboard summary");
                return StatusCode(500, new { message = "An error occurred while retrieving dashboard summary" });
            }
        }

        #endregion

        #region User Analytics

        [HttpGet("analytics/user-growth")]
        public async Task<ActionResult<List<UserGrowthTrendDto>>> GetUserGrowthTrend(
            [FromQuery] int days = 30)
        {
            try
            {
                var result = await _dashboardService.GetUserGrowthTrendAsync(days);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving user growth trend");
                return StatusCode(500, new { message = "An error occurred" });
            }
        }

        [HttpGet("analytics/users-by-department")]
        public async Task<ActionResult<List<UsersByDepartmentDto>>> GetUsersByDepartment()
        {
            try
            {
                var result = await _dashboardService.GetUsersByDepartmentAsync();
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving users by department");
                return StatusCode(500, new { message = "An error occurred" });
            }
        }

        [HttpGet("analytics/users-by-status")]
        public async Task<ActionResult<List<UsersByStatusDto>>> GetUsersByStatus()
        {
            try
            {
                var result = await _dashboardService.GetUsersByStatusAsync();
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving users by status");
                return StatusCode(500, new { message = "An error occurred" });
            }
        }

        [HttpGet("analytics/top-users")]
        public async Task<ActionResult<List<TopActiveUsersDto>>> GetTopUsers(
            [FromQuery] int days = 30, [FromQuery] int limit = 10)
        {
            try
            {
                var result = await _dashboardService.GetTopActiveUsersAsync(days, limit);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving top users");
                return StatusCode(500, new { message = "An error occurred" });
            }
        }

        #endregion

        #region Document Analytics

        [HttpGet("analytics/document-trends")]
        public async Task<ActionResult<List<DocumentUploadTrendDto>>> GetDocumentTrends(
            [FromQuery] int days = 30)
        {
            try
            {
                var result = await _dashboardService.GetDocumentUploadTrendAsync(days);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving document trends");
                return StatusCode(500, new { message = "An error occurred" });
            }
        }

        [HttpGet("analytics/document-status")]
        public async Task<ActionResult<List<DocumentStatusDistributionDto>>> GetDocumentStatus()
        {
            try
            {
                var result = await _dashboardService.GetDocumentStatusDistributionAsync();
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving document status distribution");
                return StatusCode(500, new { message = "An error occurred" });
            }
        }

        [HttpGet("analytics/document-types")]
        public async Task<ActionResult<List<DocumentTypeDistributionDto>>> GetDocumentTypes()
        {
            try
            {
                var result = await _dashboardService.GetDocumentTypeDistributionAsync();
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving document type distribution");
                return StatusCode(500, new { message = "An error occurred" });
            }
        }

        [HttpGet("analytics/storage-usage")]
        public async Task<ActionResult<StorageUsageDto>> GetStorageUsage()
        {
            try
            {
                var result = await _dashboardService.GetStorageUsageAsync();
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving storage usage");
                return StatusCode(500, new { message = "An error occurred" });
            }
        }

        [HttpGet("analytics/processing-performance")]
        public async Task<ActionResult<List<ProcessingPerformanceDto>>> GetProcessingPerformance(
            [FromQuery] int days = 30)
        {
            try
            {
                var result = await _dashboardService.GetProcessingPerformanceAsync(days);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving processing performance");
                return StatusCode(500, new { message = "An error occurred" });
            }
        }

        [HttpGet("analytics/batch-summary")]
        public async Task<ActionResult<BatchProcessingSummaryDto>> GetBatchSummary()
        {
            try
            {
                var result = await _dashboardService.GetBatchProcessingSummaryAsync();
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving batch summary");
                return StatusCode(500, new { message = "An error occurred" });
            }
        }

        #endregion

        #region Patient Analytics

        [HttpGet("analytics/patient-conflicts")]
        public async Task<ActionResult<List<PatientConflictTrendDto>>> GetPatientConflicts(
            [FromQuery] int days = 30)
        {
            try
            {
                var result = await _dashboardService.GetPatientConflictTrendAsync(days);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving patient conflict trends");
                return StatusCode(500, new { message = "An error occurred" });
            }
        }

        #endregion

        #region Chatbot Analytics

        [HttpGet("analytics/chatbot-usage")]
        public async Task<ActionResult<List<ChatbotUsageTrendDto>>> GetChatbotUsage(
            [FromQuery] int days = 30)
        {
            try
            {
                var result = await _dashboardService.GetChatbotUsageTrendAsync(days);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving chatbot usage");
                return StatusCode(500, new { message = "An error occurred" });
            }
        }

        #endregion

        #region Security Analytics

        [HttpGet("analytics/login-activity")]
        public async Task<ActionResult<List<LoginActivityTrendDto>>> GetLoginActivity(
            [FromQuery] int days = 30)
        {
            try
            {
                var result = await _dashboardService.GetLoginActivityTrendAsync(days);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving login activity");
                return StatusCode(500, new { message = "An error occurred" });
            }
        }

        [HttpGet("analytics/security-events")]
        public async Task<ActionResult<List<SecurityEventDto>>> GetSecurityEvents(
            [FromQuery] int days = 30)
        {
            try
            {
                var result = await _dashboardService.GetSecurityEventTrendAsync(days);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving security events");
                return StatusCode(500, new { message = "An error occurred" });
            }
        }

        [HttpGet("analytics/hourly-activity")]
        public async Task<ActionResult<List<HourlyActivityDto>>> GetHourlyActivity(
            [FromQuery] int days = 30)
        {
            try
            {
                var result = await _dashboardService.GetHourlyActivityAsync(days);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving hourly activity");
                return StatusCode(500, new { message = "An error occurred" });
            }
        }

        [HttpGet("analytics/action-breakdown")]
        public async Task<ActionResult<List<AuditActionBreakdownDto>>> GetActionBreakdown(
            [FromQuery] int days = 30)
        {
            try
            {
                var result = await _dashboardService.GetAuditActionBreakdownAsync(days);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving action breakdown");
                return StatusCode(500, new { message = "An error occurred" });
            }
        }

        #endregion

        #region Helpers

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

        #endregion
    }
}
