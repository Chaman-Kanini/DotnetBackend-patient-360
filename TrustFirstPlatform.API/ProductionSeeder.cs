using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using TrustFirstPlatform.Domain.Entities;
using TrustFirstPlatform.Infrastructure.Data;

namespace TrustFirstPlatform.API;

/// <summary>
/// Database seeder for production environments
/// Run this separately to seed initial data
/// </summary>
public class ProductionSeeder
{
    private readonly string _connectionString;
    private readonly ILogger<ProductionSeeder> _logger;

    public ProductionSeeder(string connectionString, ILogger<ProductionSeeder> logger)
    {
        _connectionString = connectionString;
        _logger = logger;
    }

    public async Task SeedAsync()
    {
        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
        optionsBuilder.UseNpgsql(_connectionString);

        using var dbContext = new AppDbContext(optionsBuilder.Options);

        await SeedAdminUser(dbContext);
        await SeedClinicalCodes(dbContext);
        await SeedStandardUser(dbContext);

        await dbContext.SaveChangesAsync();

        _logger.LogInformation("Production database seeding completed successfully");
    }

    private async Task SeedAdminUser(AppDbContext dbContext)
    {
        var adminEmail = "admin@trustfirst.com";
        var adminExists = await dbContext.Users.AnyAsync(u => u.Email == adminEmail);

        if (!adminExists)
        {
            var adminUser = new User
            {
                Id = Guid.NewGuid(),
                Email = adminEmail,
                PasswordHash = PasswordHasher.HashPassword("Admin@123"), // Change this password after first login
                Role = "Admin",
                FailedLoginAttempts = 0,
                LockoutEnd = null,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                LastLoginAt = null,
                Profile = JsonDocument.Parse("{}"),
                Status = "Active",
                ApprovedAt = DateTime.UtcNow,
                ApprovedBy = null,
                FirstName = "System",
                LastName = "Administrator",
                PhoneNumber = null,
                Department = "IT"
            };

            dbContext.Users.Add(adminUser);

            var auditLog = new AuditLog
            {
                Id = Guid.NewGuid(),
                UserId = adminUser.Id,
                Action = "ADMIN_USER_SEEDED",
                OccurredAt = DateTime.UtcNow,
                IpAddress = "127.0.0.1",
                Metadata = JsonDocument.Parse(JsonSerializer.Serialize(new { email = adminEmail, seededBy = "ProductionSeeder" }))
            };

            dbContext.AuditLogs.Add(auditLog);

            _logger.LogInformation("✅ Admin user created: {Email} (Password: Admin@123)", adminEmail);
        }
        else
        {
            _logger.LogInformation("ℹ️  Admin user already exists: {Email}", adminEmail);
        }
    }

    private async Task SeedStandardUser(AppDbContext dbContext)
    {
        var standardEmail = "user@trustfirst.com";
        var standardExists = await dbContext.Users.AnyAsync(u => u.Email == standardEmail);

        if (!standardExists)
        {
            var standardUser = new User
            {
                Id = Guid.NewGuid(),
                Email = standardEmail,
                PasswordHash = PasswordHasher.HashPassword("User@123"), // Change this password after first login
                Role = "StandardUser",
                FailedLoginAttempts = 0,
                LockoutEnd = null,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                LastLoginAt = null,
                Profile = JsonDocument.Parse("{}"),
                Status = "Active",
                ApprovedAt = DateTime.UtcNow,
                ApprovedBy = null,
                FirstName = "Standard",
                LastName = "User",
                PhoneNumber = null,
                Department = "General"
            };

            dbContext.Users.Add(standardUser);

            var auditLog = new AuditLog
            {
                Id = Guid.NewGuid(),
                UserId = standardUser.Id,
                Action = "STANDARD_USER_SEEDED",
                OccurredAt = DateTime.UtcNow,
                IpAddress = "127.0.0.1",
                Metadata = JsonDocument.Parse(JsonSerializer.Serialize(new { email = standardEmail, seededBy = "ProductionSeeder" }))
            };

            dbContext.AuditLogs.Add(auditLog);

            _logger.LogInformation("✅ Standard user created: {Email} (Password: User@123)", standardEmail);
        }
        else
        {
            _logger.LogInformation("ℹ️  Standard user already exists: {Email}", standardEmail);
        }
    }

    private async Task SeedClinicalCodes(AppDbContext dbContext)
    {
        // Seed ICD-10 codes
        var icd10Exists = await dbContext.ICD10Codes.AnyAsync();
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
                new ICD10Code { Code = "Z87.891", Diagnosis = "Personal history of nicotine dependence" },
                new ICD10Code { Code = "I50.9", Diagnosis = "Heart failure, unspecified" },
                new ICD10Code { Code = "N39.0", Diagnosis = "Urinary tract infection, site not specified" },
                new ICD10Code { Code = "M54.5", Diagnosis = "Low back pain" },
                new ICD10Code { Code = "R11.10", Diagnosis = "Vomiting, unspecified" },
                new ICD10Code { Code = "K59.00", Diagnosis = "Constipation, unspecified" }
            };

            dbContext.ICD10Codes.AddRange(icd10Codes);
            _logger.LogInformation("✅ Seeded {Count} ICD-10 codes", icd10Codes.Length);
        }
        else
        {
            _logger.LogInformation("ℹ️  ICD-10 codes already exist");
        }

        // Seed CPT codes
        var cptExists = await dbContext.CPTCodes.AnyAsync();
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
                new CPTCode { Code = "29881", Procedure = "Arthroscopy, knee, surgical; with meniscectomy" },
                new CPTCode { Code = "99232", Procedure = "Subsequent hospital care, per day, for the evaluation and management of a patient" },
                new CPTCode { Code = "99291", Procedure = "Critical care, evaluation and management of the critically ill or critically injured patient" },
                new CPTCode { Code = "10060", Procedure = "Incision and drainage of abscess" },
                new CPTCode { Code = "90834", Procedure = "Psychotherapy, 45 minutes" },
                new CPTCode { Code = "97110", Procedure = "Therapeutic procedure, 1 or more areas, each 15 minutes; therapeutic exercises" }
            };

            dbContext.CPTCodes.AddRange(cptCodes);
            _logger.LogInformation("✅ Seeded {Count} CPT codes", cptCodes.Length);
        }
        else
        {
            _logger.LogInformation("ℹ️  CPT codes already exist");
        }
    }
}
