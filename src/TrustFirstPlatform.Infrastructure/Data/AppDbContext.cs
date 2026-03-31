using Microsoft.EntityFrameworkCore;
using System;
using TrustFirstPlatform.Domain.Entities;

namespace TrustFirstPlatform.Infrastructure.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        public DbSet<User> Users { get; set; }
        public DbSet<UserSession> UserSessions { get; set; }
        public DbSet<PasswordResetToken> PasswordResetTokens { get; set; }
        public DbSet<AuditLog> AuditLogs { get; set; }
        public DbSet<ClinicalDocument> ClinicalDocuments { get; set; }
        public DbSet<PatientContext> PatientContexts { get; set; }
        public DbSet<UploadBatch> UploadBatches { get; set; }
        public DbSet<ICD10Code> ICD10Codes { get; set; }
        public DbSet<CPTCode> CPTCodes { get; set; }
        public DbSet<ChatHistory> ChatHistories { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // User entity configuration
            modelBuilder.Entity<User>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Email).IsRequired().HasMaxLength(255);
                entity.Property(e => e.PasswordHash).IsRequired().HasMaxLength(500);
                entity.Property(e => e.Role).IsRequired().HasMaxLength(50);
                entity.Property(e => e.CreatedAt).IsRequired();
                entity.Property(e => e.Profile).HasColumnType("jsonb");
                
                // New fields for US_002
                entity.Property(e => e.Status).HasMaxLength(20);
                entity.Property(e => e.FirstName).HasMaxLength(100);
                entity.Property(e => e.LastName).HasMaxLength(100);
                entity.Property(e => e.PhoneNumber).HasMaxLength(20);
                entity.Property(e => e.Department).HasMaxLength(100);
                entity.Property(e => e.DeactivationReason).HasMaxLength(500);
                
                // Indexes for performance
                entity.HasIndex(e => e.Email).IsUnique();
                entity.HasIndex(e => e.IsActive);
                entity.HasIndex(e => e.Status);
                entity.HasIndex(e => e.Role);
                
                // Foreign key relationships
                entity.HasOne<User>()
                      .WithMany()
                      .HasForeignKey(e => e.ApprovedBy)
                      .OnDelete(DeleteBehavior.SetNull);
                      
                entity.HasOne<User>()
                      .WithMany()
                      .HasForeignKey(e => e.DeactivatedBy)
                      .OnDelete(DeleteBehavior.SetNull);
            });

            // UserSession entity configuration
            modelBuilder.Entity<UserSession>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.UserId).IsRequired();
                entity.Property(e => e.TokenJti).IsRequired().HasMaxLength(255);
                entity.Property(e => e.CreatedAt).IsRequired();
                entity.Property(e => e.ExpiresAt).IsRequired();
                entity.Property(e => e.LastActivityAt).IsRequired();
                entity.Property(e => e.IpAddress).IsRequired().HasMaxLength(45);
                
                // Indexes for performance
                entity.HasIndex(e => e.UserId);
                entity.HasIndex(e => e.TokenJti).IsUnique();
                entity.HasIndex(e => e.ExpiresAt);
                entity.HasIndex(e => e.IsRevoked);
                
                // Foreign key relationship
                entity.HasOne(e => e.User)
                      .WithMany()
                      .HasForeignKey(e => e.UserId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            // PasswordResetToken entity configuration
            modelBuilder.Entity<PasswordResetToken>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.UserId).IsRequired();
                entity.Property(e => e.Token).IsRequired().HasMaxLength(255);
                entity.Property(e => e.ExpiresAt).IsRequired();
                
                // Indexes for performance
                entity.HasIndex(e => e.Token).IsUnique();
                entity.HasIndex(e => e.UserId);
                entity.HasIndex(e => e.ExpiresAt);
                
                // Foreign key relationship
                entity.HasOne(e => e.User)
                      .WithMany()
                      .HasForeignKey(e => e.UserId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            // AuditLog entity configuration
            modelBuilder.Entity<AuditLog>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.UserId);
                entity.Property(e => e.Action).IsRequired().HasMaxLength(100);
                entity.Property(e => e.OccurredAt).IsRequired();
                entity.Property(e => e.IpAddress).IsRequired().HasMaxLength(45);
                entity.Property(e => e.Metadata).HasColumnType("jsonb");
                
                // Indexes for performance
                entity.HasIndex(e => e.UserId);
                entity.HasIndex(e => e.Action);
                entity.HasIndex(e => e.OccurredAt);
                
                // Foreign key relationship
                entity.HasOne(e => e.User)
                      .WithMany()
                      .HasForeignKey(e => e.UserId)
                      .OnDelete(DeleteBehavior.SetNull);
            });

            // ClinicalDocument entity configuration
            modelBuilder.Entity<ClinicalDocument>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.UserId).IsRequired();
                entity.Property(e => e.PatientContextId);
                entity.Property(e => e.UploadBatchId);
                entity.Property(e => e.OriginalFileName).IsRequired().HasMaxLength(255);
                entity.Property(e => e.StoredFileName).IsRequired().HasMaxLength(255);
                entity.Property(e => e.ContentType).IsRequired().HasMaxLength(100);
                entity.Property(e => e.FileSizeBytes).IsRequired();
                entity.Property(e => e.FileExtension).IsRequired().HasMaxLength(10);
                entity.Property(e => e.StoragePath).IsRequired().HasMaxLength(500);
                entity.Property(e => e.FileHash).HasMaxLength(64);
                entity.Property(e => e.Status).IsRequired();
                entity.Property(e => e.ValidationError);
                entity.Property(e => e.ProcessingError);
                entity.Property(e => e.ExtractedText);
                entity.Property(e => e.ExtractedData).HasColumnType("jsonb");
                entity.Property(e => e.UploadedAt).IsRequired();
                entity.Property(e => e.ProcessedAt);
                entity.Property(e => e.Metadata).HasColumnType("jsonb");

                entity.HasIndex(e => e.UserId);
                entity.HasIndex(e => e.PatientContextId);
                entity.HasIndex(e => e.UploadBatchId);
                entity.HasIndex(e => e.Status);
                entity.HasIndex(e => e.UploadedAt);
                entity.HasIndex(e => e.FileHash);

                entity.HasOne(e => e.User)
                      .WithMany()
                      .HasForeignKey(e => e.UserId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(e => e.PatientContext)
                      .WithMany()
                      .HasForeignKey(e => e.PatientContextId)
                      .OnDelete(DeleteBehavior.SetNull);

                entity.HasOne(e => e.UploadBatch)
                      .WithMany(b => b.Documents)
                      .HasForeignKey(e => e.UploadBatchId)
                      .OnDelete(DeleteBehavior.SetNull);
            });

            // PatientContext entity configuration
            modelBuilder.Entity<PatientContext>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.PatientIdentifier).IsRequired().HasMaxLength(100);
                entity.Property(e => e.PatientName).IsRequired().HasMaxLength(255);
                entity.Property(e => e.CreatedAt).IsRequired();
                entity.Property(e => e.CreatedByUserId).IsRequired();
                entity.Property(e => e.ConsolidatedData).HasColumnType("jsonb");
                entity.Property(e => e.Status).HasMaxLength(50);
                entity.Property(e => e.LastConsolidatedAt);
                entity.Property(e => e.HasConflicts).IsRequired();
                entity.Property(e => e.ConflictCount).IsRequired();

                entity.HasIndex(e => e.PatientIdentifier).IsUnique();
                entity.HasIndex(e => e.CreatedByUserId);
                entity.HasIndex(e => e.CreatedAt);
                entity.HasIndex(e => e.Status);
                entity.HasIndex(e => e.HasConflicts);

                entity.HasOne(e => e.CreatedByUser)
                      .WithMany()
                      .HasForeignKey(e => e.CreatedByUserId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            // ICD10Code entity configuration - standalone reference table
            modelBuilder.Entity<ICD10Code>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Code).IsRequired().HasMaxLength(20);
                entity.Property(e => e.Diagnosis).IsRequired().HasMaxLength(500);

                entity.HasIndex(e => e.Code);
            });

            // CPTCode entity configuration - standalone reference table
            modelBuilder.Entity<CPTCode>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Code).IsRequired().HasMaxLength(20);
                entity.Property(e => e.Procedure).IsRequired().HasMaxLength(500);

                entity.HasIndex(e => e.Code);
            });

            // UploadBatch entity configuration
            modelBuilder.Entity<UploadBatch>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.UserId).IsRequired();
                entity.Property(e => e.CreatedAt).IsRequired();
                entity.Property(e => e.TotalDocuments).IsRequired();
                entity.Property(e => e.ProcessedDocuments).IsRequired();
                entity.Property(e => e.Status).IsRequired().HasMaxLength(50);

                entity.HasIndex(e => e.UserId);
                entity.HasIndex(e => e.Status);
                entity.HasIndex(e => e.CreatedAt);

                entity.HasOne(e => e.User)
                      .WithMany()
                      .HasForeignKey(e => e.UserId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            // ChatHistory entity configuration
            modelBuilder.Entity<ChatHistory>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.UserId).IsRequired();
                entity.Property(e => e.BatchId).HasMaxLength(100);
                entity.Property(e => e.Question).IsRequired();
                entity.Property(e => e.Answer).IsRequired();
                entity.Property(e => e.Timestamp).IsRequired();

                entity.HasIndex(e => e.UserId);
                entity.HasIndex(e => e.BatchId);
                entity.HasIndex(e => e.Timestamp);

                entity.HasOne(e => e.User)
                      .WithMany()
                      .HasForeignKey(e => e.UserId)
                      .OnDelete(DeleteBehavior.Cascade);
            });
        }
    }
}
