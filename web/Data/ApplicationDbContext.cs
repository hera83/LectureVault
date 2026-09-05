using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using web.Data.Entities;

namespace web.Data
{
    /// <summary>
    /// Application database context for Identity, app settings, themes and file metadata
    /// </summary>
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        // App settings
        public DbSet<AppSetting> AppSettings { get; set; } = null!;

        // Theme settings
        public DbSet<ThemeSetting> ThemeSettings { get; set; } = null!;

        // File metadata (files themselves stored in App_files/)
        public DbSet<FileMetadata> FileMetadata { get; set; } = null!;

        // SMS messages (sent and received via the SMS gateway service)
        public DbSet<SmsMessage> SmsMessages { get; set; } = null!;

        // Lectures (audio recordings are attached via FileMetadata.LectureId)
        public DbSet<Lecture> Lectures { get; set; } = null!;

        // Transcription runs per lecture (versioned) and their per-file segments
        public DbSet<TranscriptionVersion> TranscriptionVersions { get; set; } = null!;
        public DbSet<TranscriptionSegment> TranscriptionSegments { get; set; } = null!;

        // Background transcription jobs, processed by TranscriptionWorker
        public DbSet<TranscriptionJob> TranscriptionJobs { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            // Configure ApplicationUser
            builder.Entity<ApplicationUser>(entity =>
            {
                entity.Property(e => e.DisplayName)
                    .IsRequired()
                    .HasMaxLength(100);

                entity.Property(e => e.ThemePreference)
                    .HasMaxLength(10);

                entity.Property(e => e.CreatedAtUtc)
                    .IsRequired();

                entity.HasIndex(e => e.Email)
                    .IsUnique();
            });

            // Configure AppSetting
            builder.Entity<AppSetting>(entity =>
            {
                entity.HasKey(e => e.Key);

                entity.Property(e => e.Key)
                    .IsRequired()
                    .HasMaxLength(100);

                entity.Property(e => e.Value)
                    .IsRequired();
            });

            // Configure ThemeSetting
            builder.Entity<ThemeSetting>(entity =>
            {
                entity.HasKey(e => e.Id);

                entity.Property(e => e.Name)
                    .IsRequired()
                    .HasMaxLength(50);

                entity.Property(e => e.ThemeMode)
                    .IsRequired()
                    .HasMaxLength(20);

                entity.HasIndex(e => new { e.Name, e.ThemeMode })
                    .IsUnique();
            });

            // Configure FileMetadata
            builder.Entity<FileMetadata>(entity =>
            {
                entity.HasKey(e => e.Id);

                entity.Property(e => e.OriginalFileName)
                    .IsRequired()
                    .HasMaxLength(255);

                entity.Property(e => e.StoredFileName)
                    .IsRequired()
                    .HasMaxLength(255);

                entity.Property(e => e.ContentType)
                    .IsRequired()
                    .HasMaxLength(100);

                entity.Property(e => e.Category)
                    .HasMaxLength(50);

                entity.Property(e => e.CreatedAtUtc)
                    .IsRequired();

                // Optional: Relationship to ApplicationUser (owner)
                // entity.HasOne<ApplicationUser>()
                //     .WithMany()
                //     .HasForeignKey(e => e.OwnerId)
                //     .OnDelete(DeleteBehavior.SetNull);

                entity.HasIndex(e => e.LectureId);

                entity.HasOne<Lecture>()
                    .WithMany()
                    .HasForeignKey(e => e.LectureId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // Configure Lecture
            builder.Entity<Lecture>(entity =>
            {
                entity.HasKey(e => e.Id);

                entity.Property(e => e.Name)
                    .IsRequired()
                    .HasMaxLength(200);

                entity.Property(e => e.OwnerId)
                    .IsRequired();

                entity.Property(e => e.CreatedAtUtc)
                    .IsRequired();

                entity.HasIndex(e => e.OwnerId);
            });

            // Configure TranscriptionVersion
            builder.Entity<TranscriptionVersion>(entity =>
            {
                entity.HasKey(e => e.Id);

                entity.Property(e => e.Model)
                    .IsRequired()
                    .HasMaxLength(200);

                entity.Property(e => e.Language)
                    .HasMaxLength(10);

                entity.Property(e => e.CreatedAtUtc)
                    .IsRequired();

                entity.HasIndex(e => new { e.LectureId, e.VersionNumber })
                    .IsUnique();

                entity.HasOne<Lecture>()
                    .WithMany()
                    .HasForeignKey(e => e.LectureId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // Configure TranscriptionSegment
            builder.Entity<TranscriptionSegment>(entity =>
            {
                entity.HasKey(e => e.Id);

                entity.Property(e => e.OriginalFileName)
                    .IsRequired()
                    .HasMaxLength(255);

                entity.Property(e => e.Text)
                    .IsRequired();

                entity.Property(e => e.CreatedAtUtc)
                    .IsRequired();

                entity.HasIndex(e => e.TranscriptionVersionId);

                entity.HasOne<TranscriptionVersion>()
                    .WithMany()
                    .HasForeignKey(e => e.TranscriptionVersionId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne<FileMetadata>()
                    .WithMany()
                    .HasForeignKey(e => e.FileMetadataId)
                    .OnDelete(DeleteBehavior.SetNull);
            });

            // Configure TranscriptionJob
            builder.Entity<TranscriptionJob>(entity =>
            {
                entity.HasKey(e => e.Id);

                entity.Property(e => e.OwnerId)
                    .IsRequired();

                entity.Property(e => e.Model)
                    .IsRequired()
                    .HasMaxLength(200);

                entity.Property(e => e.Language)
                    .HasMaxLength(10);

                entity.Property(e => e.Status)
                    .IsRequired()
                    .HasMaxLength(20);

                entity.Property(e => e.CreatedAtUtc)
                    .IsRequired();

                entity.HasIndex(e => new { e.LectureId, e.Status });

                entity.HasOne<Lecture>()
                    .WithMany()
                    .HasForeignKey(e => e.LectureId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // Configure SmsMessage
            builder.Entity<SmsMessage>(entity =>
            {
                entity.HasKey(e => e.Id);

                entity.Property(e => e.Direction)
                    .IsRequired()
                    .HasMaxLength(20);

                entity.Property(e => e.PhoneNumber)
                    .IsRequired()
                    .HasMaxLength(32);

                entity.Property(e => e.Body)
                    .IsRequired();

                entity.Property(e => e.Status)
                    .IsRequired()
                    .HasMaxLength(30);

                entity.Property(e => e.FailureReason)
                    .HasMaxLength(500);

                entity.Property(e => e.CreatedAtUtc)
                    .IsRequired();

                entity.HasIndex(e => e.GatewayMessageId);
                entity.HasIndex(e => e.PhoneNumber);
                entity.HasIndex(e => e.CreatedAtUtc);
            });
        }
    }
}
