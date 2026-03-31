using System;

namespace TrustFirstPlatform.Domain.Entities
{
    public class PasswordResetToken
    {
        public Guid Id { get; set; }
        
        public Guid UserId { get; set; }
        
        public string Token { get; set; } = string.Empty;
        
        public DateTime ExpiresAt { get; set; }
        
        public bool IsUsed { get; set; }
        
        // Navigation property
        public User User { get; set; } = null!;
    }
}
