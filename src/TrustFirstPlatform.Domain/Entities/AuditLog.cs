using System;
using System.Text.Json;

namespace TrustFirstPlatform.Domain.Entities
{
    public class AuditLog
    {
        public Guid Id { get; set; }
        
        public Guid? UserId { get; set; }
        
        public string Action { get; set; } = string.Empty;
        
        public DateTime OccurredAt { get; set; }
        
        public string IpAddress { get; set; } = string.Empty;
        
        public JsonDocument Metadata { get; set; } = null!; // JSONB
        
        // Navigation property
        public User? User { get; set; }
    }
}
