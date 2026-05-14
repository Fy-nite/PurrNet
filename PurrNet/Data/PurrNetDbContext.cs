using Microsoft.EntityFrameworkCore;
using Purrnet.Models;

namespace Purrnet.Data
{
    public class PurrNetDbContext : DbContext
    {
        public PurrNetDbContext(DbContextOptions<PurrNetDbContext> options) : base(options)
        {
        }

        public DbSet<Package> Packages { get; set; } = null!;
        public DbSet<User> Users { get; set; } = null!;
        public DbSet<Category> Categories { get; set; } = null!;
        public DbSet<PackageReview> PackageReviews { get; set; } = null!;
        public DbSet<AdminActivityEntity> AdminActivities { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Package>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.Name).IsUnique();
            });

            modelBuilder.Entity<User>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.GitHubId).IsUnique();
                entity.HasIndex(e => e.Username).IsUnique();
            });

            modelBuilder.Entity<PackageReview>(entity =>
            {
                entity.HasKey(e => e.Id);
            });

            modelBuilder.Entity<AdminActivityEntity>(entity =>
            {
                entity.HasKey(e => e.Id);
            });
        }

        public async Task SeedDefaultCategoriesAsync()
        {
            if (await Categories.AnyAsync()) return;
            var defaults = new[] { "Utility", "Development", "CLI", "Tools", "UI", "Library" };
            Categories.AddRange(defaults.Select(n => new Category { Name = n }));
            await SaveChangesAsync();
        }
    }
}
