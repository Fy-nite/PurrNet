using Microsoft.EntityFrameworkCore;
using Purrnet.Models;
using System.Text.Json;

namespace Purrnet.Data
{
    public class PurrDbContext : DbContext
    {
        public PurrDbContext(DbContextOptions<PurrDbContext> options) : base(options) { }
        public DbSet<Package> Packages { get; set; }
        public DbSet<Category> Categories { get; set; }
        public DbSet<User> Users { get; set; }
        public DbSet<AdminActivityEntity> AdminActivities { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Package>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.Name).IsUnique();
                entity.HasIndex(e => e.CreatedAt);
                entity.HasIndex(e => e.LastUpdated);
                entity.HasIndex(e => e.Downloads);
                entity.HasIndex(e => e.ViewCount);
                entity.HasIndex(e => e.IsActive);

                // Convert lists to JSON strings for storage
                entity.Property(e => e.Authors)
                    .HasConversion(
                        v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                        v => JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions?)null) ?? new List<string>());

                entity.Property(e => e.SupportedPlatforms)
                    .HasConversion(
                        v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                        v => JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions?)null) ?? new List<string>());

                entity.Property(e => e.Keywords)
                    .HasConversion(
                        v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                        v => JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions?)null) ?? new List<string>());

                // Add categories conversion
                entity.Property(e => e.Categories)
                    .HasConversion(
                        v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                        v => JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions?)null) ?? new List<string>());

                entity.Property(e => e.Dependencies)
                    .HasConversion(
                        v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                        v => JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions?)null) ?? new List<string>());

                entity.Property(e => e.Name).HasMaxLength(200);
                entity.Property(e => e.Version).HasMaxLength(50);
                entity.Property(e => e.Description).HasMaxLength(500);
                entity.Property(e => e.License).HasMaxLength(100);
                entity.Property(e => e.CreatedBy).HasMaxLength(100);
                entity.Property(e => e.UpdatedBy).HasMaxLength(100);

                // User relationships
                entity.HasOne(e => e.Owner)
                    .WithMany(u => u.OwnedPackages)
                    .HasForeignKey(e => e.OwnerId)
                    .OnDelete(DeleteBehavior.SetNull);

                entity.HasMany(e => e.Maintainers)
                    .WithMany(u => u.MaintainedPackages)
                    .UsingEntity<Dictionary<string, object>>(
                        "PackageMaintainer",
                        j => j.HasOne<User>().WithMany().HasForeignKey("UserId"),
                        j => j.HasOne<Package>().WithMany().HasForeignKey("PackageId"));

                // Many-to-many: Package <-> Category
                entity.HasMany(e => e.CategoryEntities)
                    .WithMany(c => c.Packages)
                    .UsingEntity<Dictionary<string, object>>("PackageCategory",
                        j => j.HasOne<Category>().WithMany().HasForeignKey("CategoryId"),
                        j => j.HasOne<Package>().WithMany().HasForeignKey("PackageId"));
            });

            modelBuilder.Entity<User>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.GitHubId).IsUnique();
                entity.HasIndex(e => e.Username).IsUnique();
                entity.HasIndex(e => e.Email);
                
                entity.Property(e => e.GitHubId).HasMaxLength(50);
                entity.Property(e => e.Username).HasMaxLength(100);
                entity.Property(e => e.Email).HasMaxLength(255);
                entity.Property(e => e.AvatarUrl).HasMaxLength(500);
            });

            modelBuilder.Entity<AdminActivityEntity>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.Timestamp);
                entity.HasIndex(e => e.UserId);

                entity.Property(e => e.Action).HasMaxLength(100);
                entity.Property(e => e.EntityType).HasMaxLength(100);
                entity.Property(e => e.EntityId).HasMaxLength(200);
                entity.Property(e => e.Details).HasMaxLength(500);

                // User relationship
                entity.HasOne(e => e.User)
                    .WithMany(u => u.AdminActivities)
                    .HasForeignKey(e => e.UserId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            base.OnModelCreating(modelBuilder);
        }
    }
}
