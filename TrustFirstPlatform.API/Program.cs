using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Npgsql;
using System.Text;
using System.Text.Json;
using TrustFirstPlatform.Infrastructure.Data;
using TrustFirstPlatform.Application.Services;
using TrustFirstPlatform.Application.Validators;
using TrustFirstPlatform.Domain.Entities;
using TrustFirstPlatform.Infrastructure.Email;
using TrustFirstPlatform.API.Middleware;
using TrustFirstPlatform.API.Services;
using FluentValidation;
using FluentValidation.AspNetCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();

builder.Services.AddValidatorsFromAssemblyContaining<LoginRequestValidator>();
builder.Services.AddFluentValidationAutoValidation();
builder.Services.AddFluentValidationClientsideAdapters();

// Database configuration
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection"),
        b => b.MigrationsAssembly("TrustFirstPlatform.API")));

// JWT Authentication configuration
var jwtSettings = builder.Configuration.GetSection("JwtSettings");
var secretKey = jwtSettings["SecretKey"];
var key = Encoding.UTF8.GetBytes(secretKey!);

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.RequireHttpsMetadata = false;
    options.SaveToken = true;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtSettings["Issuer"],
        ValidAudience = jwtSettings["Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(key),
        ClockSkew = TimeSpan.Zero
    };
});

// Authorization policies
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", policy => 
        policy.RequireRole("Admin"));
    
    options.AddPolicy("AdminOrStandardUser", policy => 
        policy.RequireRole("Admin", "StandardUser"));
});

// CORS configuration - Allow all origins
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowReactApp", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

// Register application services
builder.Services.AddSingleton<JwtTokenService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IPasswordResetService, PasswordResetService>();
builder.Services.AddScoped<IAuditService, AuditService>();
builder.Services.AddScoped<IUserManagementService, UserManagementService>();
builder.Services.AddScoped<IRegistrationService, RegistrationService>();

builder.Services.AddScoped<IFileValidationService, FileValidationService>();
builder.Services.AddScoped<IDocumentStorageService, DocumentStorageService>();
builder.Services.AddScoped<IDocumentUploadService, DocumentUploadService>();
builder.Services.AddScoped<IPythonTextExtractionService, PythonTextExtractionService>();

// Register HttpClient for Azure OpenAI
builder.Services.AddHttpClient<IClinicalExtractionService, ClinicalExtractionService>()
    .ConfigureHttpClient(client =>
    {
        client.Timeout = TimeSpan.FromSeconds(600);
    });

builder.Services.AddHttpClient<IConsolidationService, ConsolidationService>()
    .ConfigureHttpClient(client =>
    {
        client.Timeout = TimeSpan.FromSeconds(600);
    });

builder.Services.AddHttpClient<IPatientChatbotService, PatientChatbotService>()
    .ConfigureHttpClient(client =>
    {
        client.Timeout = TimeSpan.FromSeconds(120);
    });

builder.Services.AddScoped<IMedicalCodeLookupService, MedicalCodeLookupService>();
builder.Services.AddScoped<IConflictService, ConflictService>();
builder.Services.AddScoped<IDocumentProcessingService, DocumentProcessingService>();
builder.Services.AddScoped<IBatchProcessingService, BatchProcessingService>();
builder.Services.AddScoped<IAdminDashboardService, AdminDashboardService>();

// Configure email settings
builder.Services.Configure<EmailSettings>(builder.Configuration.GetSection("EmailSettings"));
builder.Services.AddScoped<IEmailService, EmailService>();

// Configure rate limiting
builder.Services.Configure<RateLimitOptions>(builder.Configuration.GetSection("RateLimitOptions"));

// Add session cleanup service
builder.Services.AddHostedService<SessionCleanupService>();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
    if (string.IsNullOrWhiteSpace(connectionString))
    {
        throw new InvalidOperationException("Connection string 'DefaultConnection' is not configured.");
    }

    var csb = new NpgsqlConnectionStringBuilder(connectionString);
    var targetDatabaseName = csb.Database;
    if (string.IsNullOrWhiteSpace(targetDatabaseName))
    {
        throw new InvalidOperationException("DefaultConnection does not specify a database name.");
    }

    var adminCsb = new NpgsqlConnectionStringBuilder(connectionString)
    {
        Database = "postgres"
    };

    using (var adminConnection = new NpgsqlConnection(adminCsb.ConnectionString))
    {
        adminConnection.Open();

        using var existsCommand = new NpgsqlCommand(
            "SELECT 1 FROM pg_database WHERE datname = @name",
            adminConnection);
        existsCommand.Parameters.AddWithValue("name", targetDatabaseName);

        var exists = existsCommand.ExecuteScalar() != null;
        if (!exists)
        {
            var escapedDbName = targetDatabaseName.Replace("\"", "\"\"");
            using var createDbCommand = new NpgsqlCommand(
                $"CREATE DATABASE \"{escapedDbName}\"",
                adminConnection);
            createDbCommand.ExecuteNonQuery();
        }
    }

    using var scope = app.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    dbContext.Database.Migrate();

    // Seed admin user
    await SeedAdminUser(dbContext, logger);
    
    // Seed clinical codes
    await SeedClinicalCodes(dbContext, logger);

    // Seed standard user (existing logic)
    var seedEmail = builder.Configuration["SeedUser:Email"];
    if (string.IsNullOrWhiteSpace(seedEmail) || string.Equals(seedEmail, "your.email@example.com", StringComparison.OrdinalIgnoreCase))
    {
        seedEmail = "standard.user@trustfirst.local";
    }

    var seedPassword = builder.Configuration["SeedUser:Password"];
    if (string.IsNullOrWhiteSpace(seedPassword))
    {
        seedPassword = "Password@123";
    }

    var seedUserId = Guid.Parse("3a0b5f7d-0d5b-4dc2-bb4e-13c8e6d8f4fb");

    var emailConflicts = dbContext.Users
        .Where(u => u.Email == seedEmail && u.Id != seedUserId)
        .ToList();
    if (emailConflicts.Count > 0)
    {
        dbContext.Users.RemoveRange(emailConflicts);
        dbContext.SaveChanges();
    }

    var seedUser = dbContext.Users.FirstOrDefault(u => u.Id == seedUserId);
    if (seedUser == null)
    {
        dbContext.Users.Add(new User
        {
            Id = seedUserId,
            Email = seedEmail,
            PasswordHash = PasswordHasher.HashPassword(seedPassword),
            Role = "StandardUser",
            FailedLoginAttempts = 0,
            LockoutEnd = null,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            LastLoginAt = null,
            Profile = JsonDocument.Parse("{}"),
        });
    }
    else
    {
        seedUser.Email = seedEmail;
        seedUser.PasswordHash = PasswordHasher.HashPassword(seedPassword);
        seedUser.Role = "StandardUser";
        seedUser.IsActive = true;
        if (seedUser.Profile == null)
        {
            seedUser.Profile = JsonDocument.Parse("{}");
        }
    }

    dbContext.SaveChanges();
}

var urls = builder.Configuration["ASPNETCORE_URLS"] ?? string.Empty;
var hasHttpsEndpoint = urls
    .Split(';', StringSplitOptions.RemoveEmptyEntries)
    .Any(u => u.StartsWith("https://", StringComparison.OrdinalIgnoreCase));

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwagger();
    app.UseSwaggerUI();
}

if (hasHttpsEndpoint || !app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

// Add exception handling middleware (should be first in the pipeline)
app.UseMiddleware<ExceptionHandlingMiddleware>();

// Add rate limiting middleware
app.UseMiddleware<RateLimitingMiddleware>();

// Add session activity tracking middleware
app.UseMiddleware<SessionActivityMiddleware>();

app.UseCors("AllowReactApp");

app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/", () => Results.Ok("TrustFirstPlatform.API is running"));

app.MapControllers();

app.Run();

// Admin user seeding method
static async Task SeedAdminUser(AppDbContext dbContext, ILogger logger)
{
    var adminEmail = "admin@trustfirst.com";
    var adminExists = await dbContext.Users.AnyAsync(u => u.Email == adminEmail);

    if (!adminExists)
    {
        var adminUser = new User
        {
            Id = Guid.NewGuid(),
            Email = adminEmail,
            PasswordHash = PasswordHasher.HashPassword("Admin@123"), // Default password - should be changed on first login
            Role = "Admin",
            FailedLoginAttempts = 0,
            LockoutEnd = null,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            LastLoginAt = null,
            Profile = JsonDocument.Parse("{}"), // Empty JSON profile
            Status = "Active",
            ApprovedAt = DateTime.UtcNow,
            ApprovedBy = null, // System created
            FirstName = "System",
            LastName = "Administrator",
            PhoneNumber = null,
            Department = "IT"
        };

        dbContext.Users.Add(adminUser);
        
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

        dbContext.AuditLogs.Add(auditLog);
        
        logger.LogInformation("Admin user created with email: {Email}", adminEmail);
    }
    else
    {
        var adminUser = await dbContext.Users.FirstAsync(u => u.Email == adminEmail);
        adminUser.PasswordHash = PasswordHasher.HashPassword("Admin@123");
        adminUser.Role = "Admin";
        adminUser.IsActive = true;
        if (adminUser.Profile == null)
        {
            adminUser.Profile = JsonDocument.Parse("{}");
        }
        logger.LogInformation("Admin user already exists with email: {Email}", adminEmail);
    }
}

// Clinical codes seeding method
static async Task SeedClinicalCodes(AppDbContext dbContext, ILogger logger)
{
    // Seed ICD-10 codes if none exist
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
        await dbContext.SaveChangesAsync();
        logger.LogInformation("Seeded {Count} ICD-10 codes", icd10Codes.Length);
    }

    // Seed CPT codes if none exist
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
        await dbContext.SaveChangesAsync();
        logger.LogInformation("Seeded {Count} CPT codes", cptCodes.Length);
    }
}
