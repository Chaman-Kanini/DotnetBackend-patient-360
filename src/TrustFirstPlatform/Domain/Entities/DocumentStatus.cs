namespace TrustFirstPlatform.Domain.Entities
{
    public enum DocumentStatus
    {
        Pending = 0,
        Validated = 1,
        Processing = 2,
        Completed = 3,
        Failed = 4,
        Rejected = 5
    }
}
