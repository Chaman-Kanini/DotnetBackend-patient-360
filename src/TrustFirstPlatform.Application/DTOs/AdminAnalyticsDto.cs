namespace TrustFirstPlatform.Application.DTOs
{
    public class DashboardSummaryDto
    {
        // User metrics
        public int TotalUsers { get; set; }
        public int ActiveUsers { get; set; }
        public int InactiveUsers { get; set; }
        public int PendingUsers { get; set; }

        // Document metrics
        public int TotalDocuments { get; set; }
        public int DocumentsToday { get; set; }
        public int DocumentsThisWeek { get; set; }
        public int DocumentsThisMonth { get; set; }

        // Patient metrics
        public int TotalPatientContexts { get; set; }
        public int PatientsWithConflicts { get; set; }
        public int TotalConflictsIdentified { get; set; }

        // Platform metrics
        public int ActiveSessions { get; set; }
        public int TotalChatbotQueries { get; set; }
        public double DocumentProcessingSuccessRate { get; set; }
        public double AvgDocumentsPerUser { get; set; }
    }

    public class UserGrowthTrendDto
    {
        public DateTime Date { get; set; }
        public int NewUsers { get; set; }
        public int TotalUsers { get; set; }
    }

    public class DocumentUploadTrendDto
    {
        public DateTime Date { get; set; }
        public int Uploaded { get; set; }
        public int Processed { get; set; }
        public int Failed { get; set; }
    }

    public class DocumentStatusDistributionDto
    {
        public string Status { get; set; } = string.Empty;
        public int Count { get; set; }
        public double Percentage { get; set; }
    }

    public class DocumentTypeDistributionDto
    {
        public string FileExtension { get; set; } = string.Empty;
        public int Count { get; set; }
        public long TotalSizeBytes { get; set; }
    }

    public class PatientConflictTrendDto
    {
        public DateTime Date { get; set; }
        public int PatientsCreated { get; set; }
        public int ConflictsDetected { get; set; }
    }

    public class ChatbotUsageTrendDto
    {
        public DateTime Date { get; set; }
        public int QueriesCount { get; set; }
        public int UniqueUsers { get; set; }
    }

    public class LoginActivityTrendDto
    {
        public DateTime Date { get; set; }
        public int SuccessfulLogins { get; set; }
        public int FailedLogins { get; set; }
        public int AccountLockouts { get; set; }
    }

    public class UsersByDepartmentDto
    {
        public string Department { get; set; } = string.Empty;
        public int Count { get; set; }
    }

    public class UsersByStatusDto
    {
        public string Status { get; set; } = string.Empty;
        public int Count { get; set; }
    }

    public class HourlyActivityDto
    {
        public int Hour { get; set; }
        public int ActionCount { get; set; }
    }

    public class ProcessingPerformanceDto
    {
        public DateTime Date { get; set; }
        public double AvgProcessingTimeMs { get; set; }
        public int DocumentsProcessed { get; set; }
    }

    public class TopActiveUsersDto
    {
        public Guid UserId { get; set; }
        public string Email { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Department { get; set; } = string.Empty;
        public int DocumentsUploaded { get; set; }
        public int PatientsCreated { get; set; }
        public int ChatbotQueries { get; set; }
        public DateTime? LastActive { get; set; }
    }

    public class SecurityEventDto
    {
        public DateTime Date { get; set; }
        public int FailedLogins { get; set; }
        public int AccountLockouts { get; set; }
        public int PasswordResets { get; set; }
        public int UserDeactivations { get; set; }
    }

    public class StorageUsageDto
    {
        public long TotalSizeBytes { get; set; }
        public int DocumentCount { get; set; }
        public double AvgFileSizeBytes { get; set; }
    }

    public class BatchProcessingSummaryDto
    {
        public int TotalBatches { get; set; }
        public int CompletedBatches { get; set; }
        public int FailedBatches { get; set; }
        public double AvgDocsPerBatch { get; set; }
    }

    public class AuditActionBreakdownDto
    {
        public string Category { get; set; } = string.Empty;
        public string Action { get; set; } = string.Empty;
        public int Count { get; set; }
    }
}
