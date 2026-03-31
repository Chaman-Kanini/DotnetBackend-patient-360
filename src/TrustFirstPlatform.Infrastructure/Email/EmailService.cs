using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;
using TrustFirstPlatform.Infrastructure.Email;

namespace TrustFirstPlatform.Infrastructure.Email
{
    public class EmailSettings
    {
        public string SmtpServer { get; set; } = string.Empty;
        public int SmtpPort { get; set; } = 587;
        public string SmtpUsername { get; set; } = string.Empty;
        public string SmtpPassword { get; set; } = string.Empty;
        public string FromEmail { get; set; } = string.Empty;
        public string FromName { get; set; } = "TrustFirst Platform";
        public bool EnableSsl { get; set; } = true;
    }

    public class EmailService : IEmailService
    {
        private readonly EmailSettings _emailSettings;
        private readonly ILogger<EmailService> _logger;

        public EmailService(IOptions<EmailSettings> emailSettings, ILogger<EmailService> logger)
        {
            _emailSettings = emailSettings.Value;
            _logger = logger;
        }

        public async Task SendPasswordResetEmailAsync(string toEmail, string resetToken, string baseUrl)
        {
            var resetUrl = $"{baseUrl.TrimEnd('/')}/reset-password?token={resetToken}";
            var subject = "TrustFirst Platform - Password Reset Request";
            
            var htmlMessage = $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <title>Password Reset</title>
    <style>
        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
        .header {{ background-color: #007AFF; color: white; padding: 20px; text-align: center; }}
        .content {{ padding: 20px; background-color: #f9f9f9; }}
        .button {{ 
            display: inline-block; 
            padding: 12px 24px; 
            background-color: #007AFF; 
            color: white; 
            text-decoration: none; 
            border-radius: 4px; 
            margin: 20px 0;
        }}
        .footer {{ padding: 20px; text-align: center; font-size: 12px; color: #666; }}
        .warning {{ background-color: #fff3cd; border: 1px solid #ffeaa7; padding: 10px; margin: 10px 0; border-radius: 4px; }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h1>TrustFirst Platform</h1>
            <p>Password Reset Request</p>
        </div>
        <div class='content'>
            <h2>Hello,</h2>
            <p>We received a request to reset your password for your TrustFirst Platform account.</p>
            
            <div class='warning'>
                <strong>Security Notice:</strong> This password reset link will expire in 1 hour for your security.
            </div>
            
            <p>Click the button below to reset your password:</p>
            
            <a href='{resetUrl}' class='button'>Reset Password</a>
            
            <p>If the button above doesn't work, you can copy and paste this link into your browser:</p>
            <p style='word-break: break-all; color: #007AFF;'><a href='{resetUrl}'>{resetUrl}</a></p>
            
            <p><strong>Important:</strong></p>
            <ul>
                <li>This link can only be used once</li>
                <li>It will expire after 1 hour</li>
                <li>If you didn't request this password reset, please ignore this email</li>
                <li>Your password will remain unchanged if you don't click the link</li>
            </ul>
        </div>
        <div class='footer'>
            <p>&copy; 2024 TrustFirst Platform. All rights reserved.</p>
            <p>This is an automated message. Please do not reply to this email.</p>
        </div>
    </div>
</body>
</html>";

            await SendEmailAsync(toEmail, subject, htmlMessage);
        }

        public async Task SendWelcomeEmailAsync(string email, string firstName, string temporaryPassword)
        {
            var subject = "Welcome to TrustFirst Platform";
            var htmlMessage = $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <title>Welcome to TrustFirst Platform</title>
    <style>
        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
        .header {{ background-color: #16A34A; color: white; padding: 20px; text-align: center; }}
        .content {{ padding: 20px; background-color: #f9f9f9; }}
        .password-box {{ 
            background-color: #f0f9ff; 
            border: 2px solid #007AFF; 
            padding: 15px; 
            margin: 20px 0; 
            border-radius: 4px; 
            font-family: monospace; 
            font-size: 18px; 
            text-align: center;
        }}
        .footer {{ padding: 20px; text-align: center; font-size: 12px; color: #666; }}
        .warning {{ background-color: #fff3cd; border: 1px solid #ffeaa7; padding: 10px; margin: 10px 0; border-radius: 4px; }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h1>TrustFirst Platform</h1>
            <p>Welcome to the Team!</p>
        </div>
        <div class='content'>
            <h2>Dear {firstName},</h2>
            <p>Welcome to the TrustFirst Clinical Intelligence Platform! Your account has been created and is ready to use.</p>
            
            <div class='warning'>
                <strong>Important:</strong> Please log in and change your password immediately for security purposes.
            </div>
            
            <p><strong>Your temporary credentials:</strong></p>
            <div class='password-box'>
                <strong>Password:</strong> {temporaryPassword}
            </div>
            
            <p>You can log in using your email address and the temporary password above.</p>
            
            <p><strong>Next Steps:</strong></p>
            <ol>
                <li>Log in to the platform with your temporary password</li>
                <li>Navigate to your profile settings</li>
                <li>Change your password to a secure one of your choice</li>
                <li>Update your profile information if needed</li>
            </ol>
            
            <p>If you have any questions or need assistance, please contact your system administrator.</p>
        </div>
        <div class='footer'>
            <p>&copy; 2024 TrustFirst Platform. All rights reserved.</p>
            <p>This is an automated message. Please do not reply to this email.</p>
        </div>
    </div>
</body>
</html>";

            await SendEmailAsync(email, subject, htmlMessage);
        }

        public async Task SendAccountApprovedEmailAsync(string email, string firstName)
        {
            var subject = "Your TrustFirst Platform Account Has Been Approved";
            var htmlMessage = $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <title>Account Approved</title>
    <style>
        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
        .header {{ background-color: #16A34A; color: white; padding: 20px; text-align: center; }}
        .content {{ padding: 20px; background-color: #f9f9f9; }}
        .button {{ 
            display: inline-block; 
            padding: 12px 24px; 
            background-color: #16A34A; 
            color: white; 
            text-decoration: none; 
            border-radius: 4px; 
            margin: 20px 0;
        }}
        .footer {{ padding: 20px; text-align: center; font-size: 12px; color: #666; }}
        .success {{ background-color: #d4edda; border: 1px solid #c3e6cb; padding: 10px; margin: 10px 0; border-radius: 4px; }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h1>TrustFirst Platform</h1>
            <p>Account Approved</p>
        </div>
        <div class='content'>
            <h2>Dear {firstName},</h2>
            
            <div class='success'>
                <strong>Good News!</strong> Your registration for the TrustFirst Clinical Intelligence Platform has been approved.
            </div>
            
            <p>Your account is now active and ready to use. You can log in to the platform and start leveraging our AI-powered clinical intelligence tools.</p>
            
            <p><strong>What you can do now:</strong></p>
            <ul>
                <li>Log in to your account</li>
                <li>Upload and process clinical documents</li>
                <li>Review AI-extracted patient data</li>
                <li>Resolve data conflicts</li>
                <li>Export finalized patient information</li>
            </ul>
            
            <p>If you have any questions or need training on how to use the platform effectively, please don't hesitate to reach out to your administrator.</p>
            
            <p>We're excited to have you join our community of healthcare professionals using TrustFirst to improve patient care through intelligent data extraction.</p>
        </div>
        <div class='footer'>
            <p>&copy; 2024 TrustFirst Platform. All rights reserved.</p>
            <p>This is an automated message. Please do not reply to this email.</p>
        </div>
    </div>
</body>
</html>";

            await SendEmailAsync(email, subject, htmlMessage);
        }

        public async Task SendAccountRejectedEmailAsync(string email, string firstName, string reason)
        {
            var subject = "Update on Your TrustFirst Platform Registration";
            var htmlMessage = $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <title>Registration Update</title>
    <style>
        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
        .header {{ background-color: #DC2626; color: white; padding: 20px; text-align: center; }}
        .content {{ padding: 20px; background-color: #f9f9f9; }}
        .footer {{ padding: 20px; text-align: center; font-size: 12px; color: #666; }}
        .reason-box {{ 
            background-color: #fee2e2; 
            border: 1px solid #fecaca; 
            padding: 15px; 
            margin: 20px 0; 
            border-radius: 4px; 
        }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h1>TrustFirst Platform</h1>
            <p>Registration Update</p>
        </div>
        <div class='content'>
            <h2>Dear {firstName},</h2>
            <p>Thank you for your interest in the TrustFirst Clinical Intelligence Platform.</p>
            
            <p>After careful review of your registration request, we are unable to approve your account at this time.</p>
            
            <div class='reason-box'>
                <strong>Reason:</strong> {reason}
            </div>
            
            <p><strong>What this means:</strong></p>
            <ul>
                <li>Your registration has been rejected</li>
                <li>You will not be able to access the platform</li>
                <li>No account has been created for you</li>
            </ul>
            
            <p><strong>Next steps:</strong></p>
            <ul>
                <li>If you believe this is an error, please contact your system administrator</li>
                <li>If you have questions about the rejection reason, reach out to your supervisor</li>
                <li>You may need to submit a new registration request if circumstances change</li>
            </ul>
            
            <p>We appreciate your understanding and wish you the best in your healthcare endeavors.</p>
        </div>
        <div class='footer'>
            <p>&copy; 2024 TrustFirst Platform. All rights reserved.</p>
            <p>This is an automated message. Please do not reply to this email.</p>
        </div>
    </div>
</body>
</html>";

            await SendEmailAsync(email, subject, htmlMessage);
        }

        public async Task SendAccountDeactivatedEmailAsync(string email, string firstName, string reason)
        {
            var subject = "Important: Your TrustFirst Platform Account Has Been Deactivated";
            var htmlMessage = $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <title>Account Deactivated</title>
    <style>
        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
        .header {{ background-color: #DC2626; color: white; padding: 20px; text-align: center; }}
        .content {{ padding: 20px; background-color: #f9f9f9; }}
        .footer {{ padding: 20px; text-align: center; font-size: 12px; color: #666; }}
        .warning {{ background-color: #fee2e2; border: 1px solid #fecaca; padding: 15px; margin: 20px 0; border-radius: 4px; }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h1>TrustFirst Platform</h1>
            <p>Account Deactivated</p>
        </div>
        <div class='content'>
            <h2>Dear {firstName},</h2>
            
            <div class='warning'>
                <strong>Important Notice:</strong> Your TrustFirst Platform account has been deactivated.
            </div>
            
            <p>Your access to the platform has been revoked effective immediately. This action was taken by your system administrator.</p>
            
            <p><strong>Reason for deactivation:</strong></p>
            <p>{reason}</p>
            
            <p><strong>What this means:</strong></p>
            <ul>
                <li>You can no longer log in to the platform</li>
                <li>All active sessions have been terminated</li>
                <li>Your access to patient data and platform features is revoked</li>
            </ul>
            
            <p><strong>If you believe this is an error:</strong></p>
            <ul>
                <li>Contact your system administrator immediately</li>
                <li>Provide your account details and explain why you believe the deactivation was in error</li>
                <li>Your administrator will review your case and take appropriate action</li>
            </ul>
            
            <p>Thank you for your understanding and cooperation.</p>
        </div>
        <div class='footer'>
            <p>&copy; 2024 TrustFirst Platform. All rights reserved.</p>
            <p>This is an automated message. Please do not reply to this email.</p>
        </div>
    </div>
</body>
</html>";

            await SendEmailAsync(email, subject, htmlMessage);
        }

        public async Task SendPendingApprovalNotificationAsync(string adminEmail, string pendingUserEmail)
        {
            var subject = "New User Registration Awaiting Approval";
            var htmlMessage = $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <title>Pending User Approval</title>
    <style>
        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
        .header {{ background-color: #F59E0B; color: white; padding: 20px; text-align: center; }}
        .content {{ padding: 20px; background-color: #f9f9f9; }}
        .button {{ 
            display: inline-block; 
            padding: 12px 24px; 
            background-color: #F59E0B; 
            color: white; 
            text-decoration: none; 
            border-radius: 4px; 
            margin: 20px 0;
        }}
        .footer {{ padding: 20px; text-align: center; font-size: 12px; color: #666; }}
        .info {{ background-color: #fff3cd; border: 1px solid #ffeaa7; padding: 10px; margin: 10px 0; border-radius: 4px; }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h1>TrustFirst Platform</h1>
            <p>Admin Notification</p>
        </div>
        <div class='content'>
            <h2>Admin Alert:</h2>
            
            <div class='info'>
                <strong>New Registration:</strong> A new user has registered and is awaiting your approval.
            </div>
            
            <p><strong>Registration Details:</strong></p>
            <ul>
                <li><strong>Email:</strong> {pendingUserEmail}</li>
                <li><strong>Status:</strong> Pending Approval</li>
                <li><strong>Registration Time:</strong> {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC</li>
            </ul>
            
            <p><strong>Action Required:</strong></p>
            <ul>
                <li>Review the user's registration information</li>
                <li>Verify their eligibility for platform access</li>
                <li>Approve or reject the registration request</li>
            </ul>
            
            <p>Please log in to the admin dashboard to review and take action on this pending registration.</p>
            
            <a href='#' class='button'>Go to Admin Dashboard</a>
            
            <p><strong>Important:</strong> Prompt action helps ensure new users can start using the platform efficiently.</p>
        </div>
        <div class='footer'>
            <p>&copy; 2024 TrustFirst Platform. All rights reserved.</p>
            <p>This is an automated message. Please do not reply to this email.</p>
        </div>
    </div>
</body>
</html>";

            await SendEmailAsync(adminEmail, subject, htmlMessage);
        }

        public async Task SendEmailAsync(string toEmail, string subject, string htmlMessage)
        {
            try
            {
                using var client = new SmtpClient(_emailSettings.SmtpServer, _emailSettings.SmtpPort)
                {
                    Credentials = new NetworkCredential(_emailSettings.SmtpUsername, _emailSettings.SmtpPassword),
                    EnableSsl = _emailSettings.EnableSsl
                };

                var mailMessage = new MailMessage
                {
                    From = new MailAddress(_emailSettings.FromEmail, _emailSettings.FromName),
                    Subject = subject,
                    Body = htmlMessage,
                    IsBodyHtml = true
                };

                mailMessage.To.Add(toEmail);

                await client.SendMailAsync(mailMessage);
                
                _logger.LogInformation("Email sent successfully to {Email} with subject {Subject}", toEmail, subject);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send email to {Email} with subject {Subject}", toEmail, subject);
                throw new InvalidOperationException($"Failed to send email: {ex.Message}", ex);
            }
        }
    }
}
