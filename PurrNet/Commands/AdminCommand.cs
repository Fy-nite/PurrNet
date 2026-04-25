using MongoDB.Driver;
using Purrnet.Data;
using Purrnet.Models;
using System.Text.Json;

namespace Purrnet.Commands
{
    public static class AdminCommand
    {
        public static async Task<int> ExecuteAsync(string[] args)
        {
            if (args.Length < 2)
            {
                PrintUsage();
                return 1;
            }

            var command = args[1].ToLower();

            try
            {
                return command switch
                {
                    "promote" => await PromoteUserAsync(args),
                    "revoke" => await RevokeAdminAsync(args),
                    "list" => await ListPackagesAsync(args),
                    "list-users" => await ListUsersAsync(args),
                    "export-packages" => await ExportPackagesAsync(args),
                    "import-packages" => await ImportPackagesAsync(args),
                    _ => PrintUnknownCommand(command)
                };
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.Error.WriteLine($"[ERROR] {ex.Message}");
                Console.ResetColor();
                return 1;
            }
        }

        // ── MongoDB Context ───────────────────────────────────────────────────────

        private static MongoDbContext GetMongoContext()
        {
            DotNetEnv.Env.TraversePath().Load();

            var connectionString =
                Environment.GetEnvironmentVariable("MONGODB_CONNECTION_STRING")
                ?? "mongodb://localhost:27017";

            var databaseName =
                Environment.GetEnvironmentVariable("MONGODB_DATABASE")
                ?? "purrnet";

            var settings = new MongoDbSettings
            {
                ConnectionString = connectionString,
                DatabaseName = databaseName
            };

            var client = new MongoClient(connectionString);
            return new MongoDbContext(client, settings);
        }

        // ── Commands ──────────────────────────────────────────────────────────────

        private static async Task<int> PromoteUserAsync(string[] args)
        {
            if (args.Length < 3)
            {
                Console.Error.WriteLine("Usage: --admin promote <username>");
                return 1;
            }

            var username = args[2];
            var ctx = GetMongoContext();

            var user = await ctx.Users.Find(u => u.Username == username).FirstOrDefaultAsync();
            if (user == null)
            {
                Console.Error.WriteLine($"User '{username}' not found.");
                return 1;
            }

            var update = Builders<User>.Update.Set(u => u.IsAdmin, true);
            await ctx.Users.UpdateOneAsync(u => u.Id == user.Id, update);

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"[OK] User '{username}' has been promoted to admin.");
            Console.ResetColor();
            return 0;
        }

        private static async Task<int> RevokeAdminAsync(string[] args)
        {
            if (args.Length < 3)
            {
                Console.Error.WriteLine("Usage: --admin revoke <username>");
                return 1;
            }

            var username = args[2];
            var ctx = GetMongoContext();

            var user = await ctx.Users.Find(u => u.Username == username).FirstOrDefaultAsync();
            if (user == null)
            {
                Console.Error.WriteLine($"User '{username}' not found.");
                return 1;
            }

            var update = Builders<User>.Update.Set(u => u.IsAdmin, false);
            await ctx.Users.UpdateOneAsync(u => u.Id == user.Id, update);

            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"[OK] Admin rights revoked from '{username}'.");
            Console.ResetColor();
            return 0;
        }

        private static async Task<int> ListPackagesAsync(string[] args)
        {
            var statusFilter = args.Length >= 3 ? args[2] : "all";
            var ctx = GetMongoContext();

            var filter = statusFilter == "all"
                ? FilterDefinition<Package>.Empty
                : Builders<Package>.Filter.Regex(
                    p => p.ApprovalStatus,
                    new MongoDB.Bson.BsonRegularExpression($"^{statusFilter}$", "i"));

            var packages = await ctx.Packages
                .Find(filter)
                .SortBy(p => p.Name)
                .ToListAsync();

            if (packages.Count == 0)
            {
                Console.WriteLine("No packages found.");
                return 0;
            }

            Console.WriteLine($"{"ID",-26} {"Name",-40} {"Status",-12} {"Active",-8} {"Downloads",-10}");
            Console.WriteLine(new string('-', 100));

            foreach (var pkg in packages)
            {
                var activeLabel = pkg.IsActive ? "Yes" : "No";
                Console.WriteLine($"{pkg.Id,-26} {pkg.Name,-40} {pkg.ApprovalStatus,-12} {activeLabel,-8} {pkg.Downloads,-10}");
            }

            Console.WriteLine($"\nTotal: {packages.Count}");
            return 0;
        }

        private static async Task<int> ListUsersAsync(string[] args)
        {
            var ctx = GetMongoContext();
            var users = await ctx.Users.Find(FilterDefinition<User>.Empty).SortBy(u => u.Username).ToListAsync();

            if (users.Count == 0)
            {
                Console.WriteLine("No users found.");
                return 0;
            }

            Console.WriteLine($"{"ID",-26} {"Username",-30} {"Admin",-8} {"Banned",-8} {"Email",-40}");
            Console.WriteLine(new string('-', 116));

            foreach (var user in users)
            {
                var adminLabel = user.IsAdmin ? "Yes" : "No";
                var bannedLabel = user.IsBanned ? "Yes" : "No";
                Console.WriteLine($"{user.Id,-26} {user.Username,-30} {adminLabel,-8} {bannedLabel,-8} {user.Email,-40}");
            }

            Console.WriteLine($"\nTotal: {users.Count}");
            return 0;
        }

        private static async Task<int> ExportPackagesAsync(string[] args)
        {
            var outputPath = args.Length >= 3 ? args[2] : "packages_export.json";
            var ctx = GetMongoContext();

            var packages = await ctx.Packages.Find(FilterDefinition<Package>.Empty).ToListAsync();

            var json = JsonSerializer.Serialize(packages, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(outputPath, json);

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"[OK] Exported {packages.Count} packages to '{outputPath}'.");
            Console.ResetColor();
            return 0;
        }

        private static async Task<int> ImportPackagesAsync(string[] args)
        {
            if (args.Length < 3)
            {
                Console.Error.WriteLine("Usage: --admin import-packages <file.json>");
                return 1;
            }

            var inputPath = args[2];
            if (!File.Exists(inputPath))
            {
                Console.Error.WriteLine($"File not found: '{inputPath}'");
                return 1;
            }

            var json = await File.ReadAllTextAsync(inputPath);
            var packages = JsonSerializer.Deserialize<List<Package>>(json);

            if (packages == null || packages.Count == 0)
            {
                Console.Error.WriteLine("No packages found in file.");
                return 1;
            }

            var ctx = GetMongoContext();
            int imported = 0;
            int skipped = 0;

            foreach (var pkg in packages)
            {
                var existing = await ctx.Packages.Find(p => p.Name == pkg.Name).FirstOrDefaultAsync();
                if (existing != null)
                {
                    skipped++;
                    continue;
                }

                // Clear the ID so MongoDB assigns a new one
                pkg.Id = null!;
                await ctx.Packages.InsertOneAsync(pkg);
                imported++;
            }

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"[OK] Import complete: {imported} imported, {skipped} skipped (already exist).");
            Console.ResetColor();
            return 0;
        }

        // ── Helpers ───────────────────────────────────────────────────────────────

        private static int PrintUnknownCommand(string command)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.Error.WriteLine($"Unknown command: '{command}'");
            Console.ResetColor();
            PrintUsage();
            return 1;
        }

        private static void PrintUsage()
        {
            Console.WriteLine("PurrNet Admin CLI");
            Console.WriteLine("Usage: purrnet --admin <command> [args]");
            Console.WriteLine();
            Console.WriteLine("Commands:");
            Console.WriteLine("  promote <username>           Promote a user to admin");
            Console.WriteLine("  revoke <username>            Revoke admin rights from a user");
            Console.WriteLine("  list [status]                List packages (all/pending/approved/rejected)");
            Console.WriteLine("  list-users                   List all registered users");
            Console.WriteLine("  export-packages [file.json]  Export packages to JSON");
            Console.WriteLine("  import-packages <file.json>  Import packages from JSON");
        }
    }
}
