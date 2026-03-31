using TrustFirstPlatform.Application.DTOs;

namespace TrustFirstPlatform.Application.Services
{
    public interface IAdminDashboardService
    {
        // Audit Logs
        Task<PagedResult<AuditLogDto>> GetAuditLogsAsync(AuditLogFilterRequest filter);
        Task<List<AuditLogDto>> ExportAuditLogsAsync(AuditLogExportRequest filter);
        Task<List<string>> GetDistinctActionsAsync();

        // Dashboard Summary
        Task<DashboardSummaryDto> GetDashboardSummaryAsync();

        // User Analytics
        Task<List<UserGrowthTrendDto>> GetUserGrowthTrendAsync(int days);
        Task<List<UsersByDepartmentDto>> GetUsersByDepartmentAsync();
        Task<List<UsersByStatusDto>> GetUsersByStatusAsync();
        Task<List<TopActiveUsersDto>> GetTopActiveUsersAsync(int days, int limit);

        // Document Analytics
        Task<List<DocumentUploadTrendDto>> GetDocumentUploadTrendAsync(int days);
        Task<List<DocumentStatusDistributionDto>> GetDocumentStatusDistributionAsync();
        Task<List<DocumentTypeDistributionDto>> GetDocumentTypeDistributionAsync();
        Task<StorageUsageDto> GetStorageUsageAsync();
        Task<List<ProcessingPerformanceDto>> GetProcessingPerformanceAsync(int days);
        Task<BatchProcessingSummaryDto> GetBatchProcessingSummaryAsync();

        // Patient Analytics
        Task<List<PatientConflictTrendDto>> GetPatientConflictTrendAsync(int days);

        // Chatbot Analytics
        Task<List<ChatbotUsageTrendDto>> GetChatbotUsageTrendAsync(int days);

        // Security Analytics
        Task<List<LoginActivityTrendDto>> GetLoginActivityTrendAsync(int days);
        Task<List<SecurityEventDto>> GetSecurityEventTrendAsync(int days);
        Task<List<HourlyActivityDto>> GetHourlyActivityAsync(int days);
        Task<List<AuditActionBreakdownDto>> GetAuditActionBreakdownAsync(int days);
    }
}
