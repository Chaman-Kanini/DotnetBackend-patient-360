namespace TrustFirstPlatform.Application.DTOs
{
    public class AuditLogDto
    {
        public Guid Id { get; set; }
        public Guid? UserId { get; set; }
        public string UserEmail { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public string Action { get; set; } = string.Empty;
        public DateTime OccurredAt { get; set; }
        public string IpAddress { get; set; } = string.Empty;
        public Dictionary<string, object>? Metadata { get; set; }
    }

    public class AuditLogFilterRequest
    {
        public string? SearchTerm { get; set; }
        public string? Action { get; set; }
        public Guid? UserId { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public string? IpAddress { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 25;
        public string SortBy { get; set; } = "OccurredAt";
        public string SortDirection { get; set; } = "desc";
    }

    public class AuditLogExportRequest
    {
        public string? SearchTerm { get; set; }
        public string? Action { get; set; }
        public Guid? UserId { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public string? IpAddress { get; set; }
    }
}
