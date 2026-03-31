using System;
using System.Text.Json;

namespace TrustFirstPlatform.Domain.Entities
{
    public class User
    {
        public Guid Id { get; set; }
        
        public string Email { get; set; } = string.Empty;
        
        public string PasswordHash { get; set; } = string.Empty;
        
        public string Role { get; set; } = string.Empty; // "Admin" | "StandardUser"
        
        public int FailedLoginAttempts { get; set; }
        
        public DateTime? LockoutEnd { get; set; }
        
        public bool IsActive { get; set; }
        
        public DateTime CreatedAt { get; set; }
        
        public DateTime? LastLoginAt { get; set; }
        
        public JsonDocument Profile { get; set; } = JsonDocument.Parse("{}"); // JSONB
        
        // New fields for US_002 - User Registration and Role-Based Management
        public string? Status { get; set; } // "Active" | "Pending" | "Inactive"
        
        public DateTime? ApprovedAt { get; set; }
        
        public Guid? ApprovedBy { get; set; }
        
        public string? FirstName { get; set; }
        
        public string? LastName { get; set; }
        
        public string? PhoneNumber { get; set; }
        
        public string? Department { get; set; }
        
        public DateTime? DeactivatedAt { get; set; }
        
        public Guid? DeactivatedBy { get; set; }
        
        public string? DeactivationReason { get; set; }
    }
}
