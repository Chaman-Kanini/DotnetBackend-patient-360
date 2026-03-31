namespace TrustFirstPlatform.Application.Services
{
    public interface IAuditService
    {
        Task LogAsync(string action, Guid? userId, object metadata, string ipAddress);
        Task LogAsync(Guid? userId, string action, object metadata);
    }
}
