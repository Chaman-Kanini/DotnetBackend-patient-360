using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Text.Json;
using System.Threading.Tasks;
using TrustFirstPlatform.Domain.Entities;

namespace TrustFirstPlatform.Infrastructure.Data
{
    public class DbSeeder
    {
        private readonly AppDbContext _context;
        private readonly ILogger<DbSeeder> _logger;

        public DbSeeder(AppDbContext context, ILogger<DbSeeder> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task SeedAsync()
        {
            try
            {
                await _context.Database.MigrateAsync();

                await SeedAdminUser();
                await SeedClinicalCodes();
                
                await _context.SaveChangesAsync();
                _logger.LogInformation("Database seeding completed successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while seeding the database");
                throw;
            }
        }

        private async Task SeedAdminUser()
        {
            var adminEmail = "admin@trustfirst.com";
            var adminExists = await _context.Users.AnyAsync(u => u.Email == adminEmail);

            if (!adminExists)
            {
                var adminUser = new User
                {
                    Id = Guid.NewGuid(),
                    Email = adminEmail,
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword("Admin@123"), // Default password - should be changed on first login
                    Role = "Admin",
                    FailedLoginAttempts = 0,
                    LockoutEnd = null,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    LastLoginAt = null,
                    Profile = JsonDocument.Parse("{}") // Empty JSON profile
                };

                _context.Users.Add(adminUser);
                
                // Log the creation of admin user for audit purposes
                var auditLog = new AuditLog
                {
                    Id = Guid.NewGuid(),
                    UserId = adminUser.Id,
                    Action = "ADMIN_USER_SEEDED",
                    OccurredAt = DateTime.UtcNow,
                    IpAddress = "127.0.0.1",
                    Metadata = JsonDocument.Parse(JsonSerializer.Serialize(new { email = adminEmail, seededBy = "System" }))
                };

                _context.AuditLogs.Add(auditLog);
                
                _logger.LogInformation("Admin user created with email: {Email}", adminEmail);
            }
            else
            {
                _logger.LogInformation("Admin user already exists with email: {Email}", adminEmail);
            }
        }

        private async Task SeedClinicalCodes()
        {
            // Seed ICD-10 codes if none exist
            var icd10Exists = await _context.ICD10Codes.AnyAsync();
            if (!icd10Exists)
            {
                var icd10Codes = new[]
                {
                    new ICD10Code { Code = "I10", Diagnosis = "Essential (primary) hypertension" },
                    new ICD10Code { Code = "E11.9", Diagnosis = "Type 2 diabetes mellitus without complications" },
                    new ICD10Code { Code = "Z51.11", Diagnosis = "Encounter for antineoplastic chemotherapy" },
                    new ICD10Code { Code = "M79.3", Diagnosis = "Panniculitis, unspecified" },
                    new ICD10Code { Code = "R06.02", Diagnosis = "Shortness of breath" },
                    new ICD10Code { Code = "N18.6", Diagnosis = "End stage renal disease" },
                    new ICD10Code { Code = "I25.10", Diagnosis = "Atherosclerotic heart disease of native coronary artery without angina pectoris" },
                    new ICD10Code { Code = "J44.1", Diagnosis = "Chronic obstructive pulmonary disease with acute exacerbation" },
                    new ICD10Code { Code = "F32.9", Diagnosis = "Major depressive disorder, single episode, unspecified" },
                    new ICD10Code { Code = "K21.9", Diagnosis = "Gastro-esophageal reflux disease without esophagitis" },
                    new ICD10Code { Code = "M25.511", Diagnosis = "Pain in right shoulder" },
                    new ICD10Code { Code = "R50.9", Diagnosis = "Fever, unspecified" },
                    new ICD10Code { Code = "G93.1", Diagnosis = "Anoxic brain damage, not elsewhere classified" },
                    new ICD10Code { Code = "D64.9", Diagnosis = "Anemia, unspecified" },
                    new ICD10Code { Code = "Z87.891", Diagnosis = "Personal history of nicotine dependence" }
                };

                _context.ICD10Codes.AddRange(icd10Codes);
                _logger.LogInformation("Seeded {Count} ICD-10 codes", icd10Codes.Length);
            }

            // Seed CPT codes if none exist
            var cptExists = await _context.CPTCodes.AnyAsync();
            if (!cptExists)
            {
                var cptCodes = new[]
                {
                    new CPTCode { Code = "99213", Procedure = "Office or other outpatient visit for the evaluation and management of an established patient" },
                    new CPTCode { Code = "99214", Procedure = "Office or other outpatient visit for the evaluation and management of an established patient" },
                    new CPTCode { Code = "45378", Procedure = "Colonoscopy, flexible; diagnostic, including collection of specimen(s) by brushing or washing" },
                    new CPTCode { Code = "45380", Procedure = "Colonoscopy, flexible; with biopsy, single or multiple" },
                    new CPTCode { Code = "93000", Procedure = "Electrocardiogram, routine ECG with at least 12 leads; with interpretation and report" },
                    new CPTCode { Code = "36415", Procedure = "Collection of venous blood by venipuncture" },
                    new CPTCode { Code = "80053", Procedure = "Comprehensive metabolic panel" },
                    new CPTCode { Code = "85025", Procedure = "Blood count; complete (CBC), automated" },
                    new CPTCode { Code = "71020", Procedure = "Radiologic examination, chest, 2 views, frontal and lateral" },
                    new CPTCode { Code = "73721", Procedure = "Magnetic resonance (eg, proton) imaging, any joint of lower extremity; without contrast material" },
                    new CPTCode { Code = "76700", Procedure = "Ultrasound, abdominal, real time with image documentation; complete" },
                    new CPTCode { Code = "90471", Procedure = "Immunization administration" },
                    new CPTCode { Code = "99281", Procedure = "Emergency department visit for the evaluation and management of a patient" },
                    new CPTCode { Code = "12001", Procedure = "Simple repair of superficial wounds of scalp, neck, axillae, external genitalia, trunk and/or extremities" },
                    new CPTCode { Code = "29881", Procedure = "Arthroscopy, knee, surgical; with meniscectomy" }
                };

                _context.CPTCodes.AddRange(cptCodes);
                _logger.LogInformation("Seeded {Count} CPT codes", cptCodes.Length);
            }
        }
    }
}
