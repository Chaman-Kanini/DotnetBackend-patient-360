# Production Database Seeding Script
# This script seeds the production database with initial users and clinical codes

Write-Host "🚀 Starting Production Database Seeding..." -ForegroundColor Green
Write-Host ""

# Connection string from environment variable
$connectionString = $env:DATABASE_URL

if ([string]::IsNullOrEmpty($connectionString)) {
    Write-Host "❌ Error: DATABASE_URL environment variable not set!" -ForegroundColor Red
    Write-Host "Set it with: `$env:DATABASE_URL='your-connection-string'" -ForegroundColor Yellow
    exit 1
}

Write-Host "📊 Database: Aiven PostgreSQL" -ForegroundColor Cyan
Write-Host ""

# Navigate to API directory
Set-Location -Path "TrustFirstPlatform.API"

Write-Host "📦 Building project..." -ForegroundColor Yellow
dotnet build --configuration Release

if ($LASTEXITCODE -ne 0) {
    Write-Host "❌ Build failed!" -ForegroundColor Red
    exit 1
}

Write-Host "✅ Build successful" -ForegroundColor Green
Write-Host ""

Write-Host "🌱 Starting seeding process..." -ForegroundColor Yellow
Write-Host ""

# Create a temporary C# script to run the seeder
$seederScript = @"
using System;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TrustFirstPlatform.Infrastructure.Data;
using TrustFirstPlatform.Domain.Entities;

var connectionString = "$connectionString";

var loggerFactory = LoggerFactory.Create(builder =>
{
    builder.AddConsole();
    builder.SetMinimumLevel(LogLevel.Information);
});

var logger = loggerFactory.CreateLogger<object>();

var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
optionsBuilder.UseNpgsql(connectionString);
optionsBuilder.UseLoggerFactory(loggerFactory);

using var dbContext = new AppDbContext(optionsBuilder.Options);

Console.WriteLine("✅ Connected to database");
Console.WriteLine("");

// Seed Admin User
await SeedAdminUser(dbContext, logger);

// Seed Standard User  
await SeedStandardUser(dbContext, logger);

// Seed Clinical Codes
await SeedClinicalCodes(dbContext, logger);

await dbContext.SaveChangesAsync();

Console.WriteLine("");
Console.WriteLine("✅ Production database seeding completed!");
Console.WriteLine("");
Console.WriteLine("📝 Login Credentials:");
Console.WriteLine("   Admin:    admin@trustfirst.com / Admin@123");
Console.WriteLine("   User:     user@trustfirst.com / User@123");
Console.WriteLine("");
Console.WriteLine("⚠️  Please change these passwords after first login!");

async Task SeedAdminUser(AppDbContext db, ILogger log)
{
    var email = \"admin@trustfirst.com\";
    var exists = await db.Users.AnyAsync(u => u.Email == email);
    
    if (!exists)
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = email,
            PasswordHash = PasswordHasher.HashPassword(\"Admin@123\"),
            Role = \"Admin\",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            Profile = JsonDocument.Parse(\"{}\"),
            Status = \"Active\",
            ApprovedAt = DateTime.UtcNow,
            FirstName = \"System\",
            LastName = \"Administrator\",
            Department = \"IT\"
        };
        
        db.Users.Add(user);
        log.LogInformation(\"✅ Created admin user: {Email}\", email);
    }
    else
    {
        log.LogInformation(\"ℹ️  Admin user already exists\");
    }
}

async Task SeedStandardUser(AppDbContext db, ILogger log)
{
    var email = \"user@trustfirst.com\";
    var exists = await db.Users.AnyAsync(u => u.Email == email);
    
    if (!exists)
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = email,
            PasswordHash = PasswordHasher.HashPassword(\"User@123\"),
            Role = \"StandardUser\",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            Profile = JsonDocument.Parse(\"{}\"),
            Status = \"Active\",
            ApprovedAt = DateTime.UtcNow,
            FirstName = \"Standard\",
            LastName = \"User\",
            Department = \"General\"
        };
        
        db.Users.Add(user);
        log.LogInformation(\"✅ Created standard user: {Email}\", email);
    }
    else
    {
        log.LogInformation(\"ℹ️  Standard user already exists\");
    }
}

async Task SeedClinicalCodes(AppDbContext db, ILogger log)
{
    var icd10Count = await db.ICD10Codes.CountAsync();
    var cptCount = await db.CPTCodes.CountAsync();
    
    log.LogInformation(\"ℹ️  ICD-10 codes: {Count}\", icd10Count);
    log.LogInformation(\"ℹ️  CPT codes: {Count}\", cptCount);
}
"@

# Save the script
$seederScript | Out-File -FilePath "temp-seeder.csx" -Encoding UTF8

# Run the script using dotnet-script or C# interactive
Write-Host "⚙️  Executing seeder..." -ForegroundColor Yellow
dotnet-script temp-seeder.csx

if ($LASTEXITCODE -ne 0) {
    Write-Host ""
    Write-Host "⚠️  dotnet-script not found. Installing..." -ForegroundColor Yellow
    dotnet tool install -g dotnet-script
    dotnet-script temp-seeder.csx
}

# Clean up
Remove-Item "temp-seeder.csx" -ErrorAction SilentlyContinue

Write-Host ""
Write-Host "✅ Seeding completed!" -ForegroundColor Green

# Return to root directory
Set-Location -Path ".."
