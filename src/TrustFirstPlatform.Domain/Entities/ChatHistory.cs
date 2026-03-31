using System;

namespace TrustFirstPlatform.Domain.Entities
{
    public class ChatHistory
    {
        public Guid Id { get; set; }
        
        public Guid UserId { get; set; }
        
        public string? BatchId { get; set; }
        
        public string Question { get; set; } = string.Empty;
        
        public string Answer { get; set; } = string.Empty;
        
        public DateTime Timestamp { get; set; }
        
        // Navigation property
        public User? User { get; set; }
    }
}
