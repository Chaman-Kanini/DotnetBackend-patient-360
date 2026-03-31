using System;

namespace TrustFirstPlatform.Domain.Entities
{
    public class PatientContext
    {
        public Guid Id { get; set; }
        public string PatientIdentifier { get; set; }
        public string PatientName { get; set; }
        public DateTime CreatedAt { get; set; }
        public Guid CreatedByUserId { get; set; }
        
        // Navigation properties
        public User CreatedByUser { get; set; }
    }
}
