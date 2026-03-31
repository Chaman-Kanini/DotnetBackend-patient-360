using System;

namespace TrustFirstPlatform.Domain.Entities
{
    public class UserSession
    {
        public Guid Id { get; set; }
        
        public Guid UserId { get; set; }
        
        public string TokenJti { get; set; } = string.Empty;
        
        public DateTime CreatedAt { get; set; }
        
        public DateTime ExpiresAt { get; set; }
        
        public DateTime LastActivityAt { get; set; }
        
        public bool IsRevoked { get; set; }
        
        public string IpAddress { get; set; } = string.Empty;
        
        // Navigation property
        public User User { get; set; } = null!;
    }
}
