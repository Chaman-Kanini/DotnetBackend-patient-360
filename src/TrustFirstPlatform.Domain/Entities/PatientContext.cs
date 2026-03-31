using System;
using System.Collections.Generic;
using System.Text.Json;

namespace TrustFirstPlatform.Domain.Entities
{
    public class PatientContext
    {
        public Guid Id { get; set; }
        public string PatientIdentifier { get; set; } = string.Empty;
        public string PatientName { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public Guid CreatedByUserId { get; set; }
        public JsonDocument? ConsolidatedData { get; set; }
        public string? Status { get; set; }
        public DateTime? LastConsolidatedAt { get; set; }
        public bool HasConflicts { get; set; }
        public int ConflictCount { get; set; }

        // Navigation properties
        public User CreatedByUser { get; set; } = null!;
    }
}
