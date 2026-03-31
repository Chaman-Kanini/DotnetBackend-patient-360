using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using System.Text.Json;
using TrustFirstPlatform.Domain.Entities;
using TrustFirstPlatform.Infrastructure.Data;

namespace TrustFirstPlatform.Application.Tests.Helpers
{
    /// <summary>
    /// SQLite-compatible DbContext that remaps PostgreSQL-specific column types (jsonb)
    /// to TEXT so that in-memory SQLite tests can run without provider errors.
    /// </summary>
    public class TestAppDbContext : AppDbContext
    {
        public TestAppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Override jsonb → TEXT for SQLite compatibility
            var jsonConverter = new ValueConverter<JsonDocument?, string?>(
                v => v != null ? v.RootElement.GetRawText() : null,
                v => !string.IsNullOrEmpty(v) ? JsonDocument.Parse(v, default) : null
            );

            modelBuilder.Entity<AuditLog>()
                .Property(e => e.Metadata)
                .HasColumnType("TEXT")
                .HasConversion(jsonConverter);

            modelBuilder.Entity<User>()
                .Property(e => e.Profile)
                .HasColumnType("TEXT")
                .HasConversion(jsonConverter);

            modelBuilder.Entity<ClinicalDocument>()
                .Property(e => e.ExtractedData)
                .HasColumnType("TEXT")
                .HasConversion(jsonConverter);

            modelBuilder.Entity<ClinicalDocument>()
                .Property(e => e.Metadata)
                .HasColumnType("TEXT")
                .HasConversion(jsonConverter);

            modelBuilder.Entity<PatientContext>()
                .Property(e => e.ConsolidatedData)
                .HasColumnType("TEXT")
                .HasConversion(jsonConverter);
        }
    }

    public static class TestDbContextFactory
    {
        public static AppDbContext CreateInMemoryContext()
        {
            // Create a unique SQLite database file for each test
            var dbPath = Path.Combine(Path.GetTempPath(), $"TestDb_{Guid.NewGuid()}.db");
            var connectionString = $"Data Source={dbPath}";
            
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite(connectionString)
                .EnableSensitiveDataLogging(false)
                .Options;

            var context = new TestAppDbContext(options);
            
            // Open connection and disable foreign keys for testing
            context.Database.OpenConnection();
            context.Database.ExecuteSqlRaw("PRAGMA foreign_keys = OFF;");
            
            // Create the database
            context.Database.EnsureCreated();
            
            // Create tables manually using the model
            CreateDatabaseSchema(context);
            
            return context;
        }

        private static void CreateDatabaseSchema(AppDbContext context)
        {
            // Create Users table
            context.Database.ExecuteSqlRaw(@"
                CREATE TABLE IF NOT EXISTS Users (
                    Id TEXT NOT NULL PRIMARY KEY,
                    Email TEXT NOT NULL,
                    PasswordHash TEXT NOT NULL,
                    Role TEXT NOT NULL,
                    IsActive INTEGER NOT NULL,
                    CreatedAt TEXT NOT NULL,
                    LastLoginAt TEXT,
                    FailedLoginAttempts INTEGER NOT NULL,
                    LockoutEnd TEXT,
                    Status TEXT,
                    FirstName TEXT,
                    LastName TEXT,
                    PhoneNumber TEXT,
                    Department TEXT,
                    DeactivationReason TEXT,
                    ApprovedAt TEXT,
                    DeactivatedAt TEXT,
                    Profile TEXT
                )");

            // Create UserSessions table
            context.Database.ExecuteSqlRaw(@"
                CREATE TABLE IF NOT EXISTS UserSessions (
                    Id TEXT NOT NULL PRIMARY KEY,
                    UserId TEXT NOT NULL,
                    TokenJti TEXT NOT NULL,
                    CreatedAt TEXT NOT NULL,
                    ExpiresAt TEXT NOT NULL,
                    LastActivityAt TEXT NOT NULL,
                    IsRevoked INTEGER NOT NULL,
                    IpAddress TEXT NOT NULL,
                    FOREIGN KEY (UserId) REFERENCES Users (Id)
                )");

            // Create PasswordResetTokens table
            context.Database.ExecuteSqlRaw(@"
                CREATE TABLE IF NOT EXISTS PasswordResetTokens (
                    Id TEXT NOT NULL PRIMARY KEY,
                    UserId TEXT NOT NULL,
                    Token TEXT NOT NULL,
                    ExpiresAt TEXT NOT NULL,
                    IsUsed INTEGER NOT NULL,
                    FOREIGN KEY (UserId) REFERENCES Users (Id)
                )");

            // Create AuditLogs table
            context.Database.ExecuteSqlRaw(@"
                CREATE TABLE IF NOT EXISTS AuditLogs (
                    Id TEXT NOT NULL PRIMARY KEY,
                    UserId TEXT,
                    Action TEXT NOT NULL,
                    OccurredAt TEXT NOT NULL,
                    IpAddress TEXT NOT NULL,
                    Metadata TEXT,
                    FOREIGN KEY (UserId) REFERENCES Users (Id)
                )");

            // Create indexes for better performance
            context.Database.ExecuteSqlRaw(@"CREATE INDEX IF NOT EXISTS IX_Users_Email ON Users (Email)");
            context.Database.ExecuteSqlRaw(@"CREATE INDEX IF NOT EXISTS IX_Users_IsActive ON Users (IsActive)");
            context.Database.ExecuteSqlRaw(@"CREATE INDEX IF NOT EXISTS IX_Users_Status ON Users (Status)");
            context.Database.ExecuteSqlRaw(@"CREATE INDEX IF NOT EXISTS IX_Users_Role ON Users (Role)");
            context.Database.ExecuteSqlRaw(@"CREATE INDEX IF NOT EXISTS IX_UserSessions_UserId ON UserSessions (UserId)");
            context.Database.ExecuteSqlRaw(@"CREATE INDEX IF NOT EXISTS IX_UserSessions_TokenJti ON UserSessions (TokenJti)");
            context.Database.ExecuteSqlRaw(@"CREATE INDEX IF NOT EXISTS IX_UserSessions_ExpiresAt ON UserSessions (ExpiresAt)");
            context.Database.ExecuteSqlRaw(@"CREATE INDEX IF NOT EXISTS IX_PasswordResetTokens_UserId ON PasswordResetTokens (UserId)");
            context.Database.ExecuteSqlRaw(@"CREATE INDEX IF NOT EXISTS IX_PasswordResetTokens_Token ON PasswordResetTokens (Token)");
            context.Database.ExecuteSqlRaw(@"CREATE INDEX IF NOT EXISTS IX_AuditLogs_UserId ON AuditLogs (UserId)");
            context.Database.ExecuteSqlRaw(@"CREATE INDEX IF NOT EXISTS IX_AuditLogs_Action ON AuditLogs (Action)");
            context.Database.ExecuteSqlRaw(@"CREATE INDEX IF NOT EXISTS IX_AuditLogs_OccurredAt ON AuditLogs (OccurredAt)");
        }
    }
}
