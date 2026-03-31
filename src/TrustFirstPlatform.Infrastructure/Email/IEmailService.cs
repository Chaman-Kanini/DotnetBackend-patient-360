using System.Threading.Tasks;

namespace TrustFirstPlatform.Infrastructure.Email
{
    public interface IEmailService
    {
        Task SendPasswordResetEmailAsync(string toEmail, string resetToken, string baseUrl);
        Task SendEmailAsync(string toEmail, string subject, string htmlMessage);
        
        // User management email methods
        Task SendWelcomeEmailAsync(string email, string firstName, string temporaryPassword);
        Task SendAccountApprovedEmailAsync(string email, string firstName);
        Task SendAccountRejectedEmailAsync(string email, string firstName, string reason);
        Task SendAccountDeactivatedEmailAsync(string email, string firstName, string reason);
        Task SendPendingApprovalNotificationAsync(string adminEmail, string pendingUserEmail);
    }
}
