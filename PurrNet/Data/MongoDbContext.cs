using MongoDB.Bson;
using MongoDB.Driver;
using Purrnet.Models;

namespace Purrnet.Data
{
    public class MongoDbContext
    {
        private readonly IMongoDatabase _database;

        public MongoDbContext(IMongoClient client, MongoDbSettings settings)
        {
            _database = client.GetDatabase(settings.DatabaseName);
            EnsureIndexes();
        }

        public IMongoCollection<Package> Packages =>
            _database.GetCollection<Package>("packages");

        public IMongoCollection<User> Users =>
            _database.GetCollection<User>("users");

        public IMongoCollection<Category> Categories =>
            _database.GetCollection<Category>("categories");

        public IMongoCollection<PackageReview> PackageReviews =>
            _database.GetCollection<PackageReview>("package_reviews");

        public IMongoCollection<AdminActivityEntity> AdminActivities =>
            _database.GetCollection<AdminActivityEntity>("admin_activities");

        private void EnsureIndexes()
        {
            // ── Packages ──────────────────────────────────────────────────────────
            var pkgIdx = Packages.Indexes;

            pkgIdx.CreateOne(new CreateIndexModel<Package>(
                Builders<Package>.IndexKeys.Ascending(p => p.Name),
                new CreateIndexOptions { Unique = true, Background = true }));

            pkgIdx.CreateOne(new CreateIndexModel<Package>(
                Builders<Package>.IndexKeys.Ascending(p => p.CreatedAt),
                new CreateIndexOptions { Background = true }));

            pkgIdx.CreateOne(new CreateIndexModel<Package>(
                Builders<Package>.IndexKeys.Descending(p => p.Downloads),
                new CreateIndexOptions { Background = true }));

            pkgIdx.CreateOne(new CreateIndexModel<Package>(
                Builders<Package>.IndexKeys.Descending(p => p.ViewCount),
                new CreateIndexOptions { Background = true }));

            pkgIdx.CreateOne(new CreateIndexModel<Package>(
                Builders<Package>.IndexKeys.Ascending(p => p.IsActive),
                new CreateIndexOptions { Background = true }));

            pkgIdx.CreateOne(new CreateIndexModel<Package>(
                Builders<Package>.IndexKeys.Ascending(p => p.ApprovalStatus),
                new CreateIndexOptions { Background = true }));

            // ── Users ─────────────────────────────────────────────────────────────
            var userIdx = Users.Indexes;

            userIdx.CreateOne(new CreateIndexModel<User>(
                Builders<User>.IndexKeys.Ascending(u => u.GitHubId),
                new CreateIndexOptions { Unique = true, Background = true }));

            userIdx.CreateOne(new CreateIndexModel<User>(
                Builders<User>.IndexKeys.Ascending(u => u.Username),
                new CreateIndexOptions { Unique = true, Background = true }));

            userIdx.CreateOne(new CreateIndexModel<User>(
                Builders<User>.IndexKeys.Ascending(u => u.Email),
                new CreateIndexOptions { Background = true }));

            // ── PackageReviews ────────────────────────────────────────────────────
            var revIdx = PackageReviews.Indexes;

            revIdx.CreateOne(new CreateIndexModel<PackageReview>(
                Builders<PackageReview>.IndexKeys.Ascending(r => r.PackageId),
                new CreateIndexOptions { Background = true }));

            revIdx.CreateOne(new CreateIndexModel<PackageReview>(
                Builders<PackageReview>.IndexKeys.Ascending(r => r.UserId),
                new CreateIndexOptions { Background = true }));

            revIdx.CreateOne(new CreateIndexModel<PackageReview>(
                Builders<PackageReview>.IndexKeys.Descending(r => r.CreatedAt),
                new CreateIndexOptions { Background = true }));

            // ── AdminActivities ───────────────────────────────────────────────────
            AdminActivities.Indexes.CreateOne(new CreateIndexModel<AdminActivityEntity>(
                Builders<AdminActivityEntity>.IndexKeys.Descending(a => a.Timestamp),
                new CreateIndexOptions { Background = true }));
        }

        /// <summary>Seeds default categories if the collection is empty.</summary>
        public async Task SeedDefaultCategoriesAsync()
        {
            var count = await Categories.CountDocumentsAsync(FilterDefinition<Category>.Empty);
            if (count > 0) return;

            var defaults = new[]
            {
                "Utility", "Development", "CLI", "Tools", "UI", "Library"
            };

            await Categories.InsertManyAsync(defaults.Select(n => new Category { Name = n }));
        }
    }
}
