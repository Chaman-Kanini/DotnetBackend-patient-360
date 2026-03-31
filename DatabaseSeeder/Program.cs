using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TrustFirstPlatform.Domain.Entities;
using TrustFirstPlatform.Infrastructure.Data;
using TrustFirstPlatform.Application.Services;

Console.WriteLine("🚀 TrustFirst Platform - Production Database Seeder");
Console.WriteLine("================================================");
Console.WriteLine("");

// Connection string from environment variable or command line argument
var connectionString = args.Length > 0 ? args[0] : Environment.GetEnvironmentVariable("DATABASE_URL");

if (string.IsNullOrEmpty(connectionString))
{
    Console.WriteLine("❌ Error: No connection string provided!");
    Console.WriteLine("Usage: dotnet run <connection_string>");
    Console.WriteLine("   OR: Set DATABASE_URL environment variable");
    return;
}

Console.WriteLine($"📊 Connecting to database...");

var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
optionsBuilder.UseNpgsql(connectionString);

using var dbContext = new AppDbContext(optionsBuilder.Options);

try
{
    // Test connection
    await dbContext.Database.CanConnectAsync();
    Console.WriteLine("✅ Connected successfully!");
    Console.WriteLine("");
}
catch (Exception ex)
{
    Console.WriteLine($"❌ Connection failed: {ex.Message}");
    return;
}

// Seed Admin User
Console.WriteLine("👤 Seeding Admin User...");
var adminEmail = "admin@trustfirst.com";
var adminExists = await dbContext.Users.AnyAsync(u => u.Email == adminEmail);

if (!adminExists)
{
    var adminUser = new User
    {
        Id = Guid.NewGuid(),
        Email = adminEmail,
        PasswordHash = PasswordHasher.HashPassword("Admin@123"),
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
    
    Console.WriteLine($"   ✅ Created admin user: {adminEmail}");
    Console.WriteLine($"   🔑 Password: Admin@123");
}
else
{
    Console.WriteLine($"   ℹ️  Admin user already exists: {adminEmail}");
}

// Seed Standard User
Console.WriteLine("");
Console.WriteLine("👤 Seeding Standard User...");
var userEmail = "user@trustfirst.com";
var userExists = await dbContext.Users.AnyAsync(u => u.Email == userEmail);

if (!userExists)
{
    var standardUser = new User
    {
        Id = Guid.NewGuid(),
        Email = userEmail,
        PasswordHash = PasswordHasher.HashPassword("User@123"),
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
    
    Console.WriteLine($"   ✅ Created standard user: {userEmail}");
    Console.WriteLine($"   🔑 Password: User@123");
}
else
{
    Console.WriteLine($"   ℹ️  Standard user already exists: {userEmail}");
}

// Save changes
try
{
    var changes = await dbContext.SaveChangesAsync();
    Console.WriteLine("");
    Console.WriteLine($"💾 Saved {changes} changes to database");
}
catch (Exception ex)
{
    Console.WriteLine("");
    Console.WriteLine($"❌ Error saving changes: {ex.Message}");
    return;
}

// Display summary
Console.WriteLine("");
Console.WriteLine("================================================");
Console.WriteLine("✅ Database seeding completed successfully!");
Console.WriteLine("================================================");
Console.WriteLine("");
Console.WriteLine("📝 Login Credentials:");
Console.WriteLine("   👨‍💼 Admin:  admin@trustfirst.com / Admin@123");
Console.WriteLine("   👤 User:   user@trustfirst.com / User@123");
Console.WriteLine("");
Console.WriteLine("⚠️  IMPORTANT: Change these passwords after first login!");
Console.WriteLine("");

// Display user statistics
var totalUsers = await dbContext.Users.CountAsync();
var adminCount = await dbContext.Users.CountAsync(u => u.Role == "Admin");
var standardCount = await dbContext.Users.CountAsync(u => u.Role == "StandardUser");

Console.WriteLine("📊 Database Statistics:");
Console.WriteLine($"   Total Users: {totalUsers}");
Console.WriteLine($"   Admins: {adminCount}");
Console.WriteLine($"   Standard Users: {standardCount}");
Console.WriteLine("");
