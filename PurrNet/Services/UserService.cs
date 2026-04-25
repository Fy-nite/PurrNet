using MongoDB.Driver;
using Purrnet.Data;
using Purrnet.Models;

namespace Purrnet.Services
{
    public class UserService : IUserService
    {
        private readonly MongoDbContext _context;
        private readonly ILogger<UserService> _logger;

        public UserService(MongoDbContext context, ILogger<UserService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<User?> GetUserByGitHubIdAsync(string gitHubId)
        {
            try
            {
                return await _context.Users.Find(u => u.GitHubId == gitHubId).FirstOrDefaultAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving user by GitHub ID {GitHubId}", gitHubId);
                return null;
            }
        }

        public async Task<User> CreateUserAsync(string gitHubId, string username, string email, string avatarUrl)
        {
            try
            {
                var user = new User
                {
                    GitHubId = gitHubId,
                    Username = username,
                    Email = email,
                    AvatarUrl = avatarUrl,
                    CreatedAt = DateTime.UtcNow,
                    LastLoginAt = DateTime.UtcNow,
                    IsAdmin = false
                };

                await _context.Users.InsertOneAsync(user);
                _logger.LogInformation("Created new user {Username} (GitHub ID: {GitHubId})", username, gitHubId);
                return user;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating user {Username}", username);
                throw;
            }
        }

        public async Task<User> UpdateUserAsync(User user)
        {
            try
            {
                user.LastLoginAt = DateTime.UtcNow;
                await _context.Users.ReplaceOneAsync(u => u.Id == user.Id, user);
                return user;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating user {Username}", user.Username);
                throw;
            }
        }

        public async Task<bool> IsAdminAsync(string userId)
        {
            try
            {
                var user = await _context.Users.Find(u => u.Id == userId).FirstOrDefaultAsync();
                return user?.IsAdmin ?? false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking admin status for user {UserId}", userId);
                return false;
            }
        }

        public async Task<List<User>> GetAllUsersAsync()
        {
            try
            {
                return await _context.Users
                    .Find(FilterDefinition<User>.Empty)
                    .SortBy(u => u.Username)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving all users");
                return new List<User>();
            }
        }

        public async Task<bool> PromoteToAdminAsync(string userId)
        {
            try
            {
                var update = Builders<User>.Update.Set(u => u.IsAdmin, true);
                var result = await _context.Users.UpdateOneAsync(u => u.Id == userId, update);
                if (result.ModifiedCount > 0)
                    _logger.LogInformation("User {UserId} promoted to admin", userId);
                return result.ModifiedCount > 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error promoting user {UserId} to admin", userId);
                return false;
            }
        }

        public async Task<bool> RevokeAdminAsync(string userId)
        {
            try
            {
                var update = Builders<User>.Update.Set(u => u.IsAdmin, false);
                var result = await _context.Users.UpdateOneAsync(u => u.Id == userId, update);
                return result.ModifiedCount > 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error revoking admin for user {UserId}", userId);
                return false;
            }
        }

        public async Task<bool> BanUserAsync(string userId)
        {
            try
            {
                var update = Builders<User>.Update.Set(u => u.IsBanned, true);
                var result = await _context.Users.UpdateOneAsync(u => u.Id == userId, update);
                if (result.ModifiedCount > 0)
                    _logger.LogInformation("User {UserId} banned", userId);
                return result.ModifiedCount > 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error banning user {UserId}", userId);
                return false;
            }
        }

        public async Task<bool> UnbanUserAsync(string userId)
        {
            try
            {
                var update = Builders<User>.Update.Set(u => u.IsBanned, false);
                var result = await _context.Users.UpdateOneAsync(u => u.Id == userId, update);
                if (result.ModifiedCount > 0)
                    _logger.LogInformation("User {UserId} unbanned", userId);
                return result.ModifiedCount > 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error unbanning user {UserId}", userId);
                return false;
            }
        }

        public async Task<User?> GetUserByUsernameAsync(string username)
        {
            try
            {
                return await _context.Users.Find(u => u.Username == username).FirstOrDefaultAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving user by username {Username}", username);
                return null;
            }
        }

        public async Task<List<Package>> GetUserPackagesAsync(string userId)
        {
            try
            {
                return await _context.Packages
                    .Find(p => p.OwnerId == userId && p.IsActive)
                    .SortByDescending(p => p.CreatedAt)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting packages for user {UserId}", userId);
                return new List<Package>();
            }
        }

        public async Task<List<Package>> GetUserMaintainedPackagesAsync(string userId)
        {
            try
            {
                var filter = Builders<Package>.Filter.And(
                    Builders<Package>.Filter.Eq(p => p.IsActive, true),
                    Builders<Package>.Filter.AnyEq("MaintainerIds", userId));

                return await _context.Packages
                    .Find(filter)
                    .SortByDescending(p => p.LastUpdated)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting maintained packages for user {UserId}", userId);
                return new List<Package>();
            }
        }

        public async Task<bool> MakeFirstUserAdminAsync()
        {
            try
            {
                var count = await _context.Users.CountDocumentsAsync(FilterDefinition<User>.Empty);
                if (count != 1) return false;

                var first = await _context.Users.Find(FilterDefinition<User>.Empty).FirstAsync();
                var update = Builders<User>.Update.Set(u => u.IsAdmin, true);
                await _context.Users.UpdateOneAsync(u => u.Id == first.Id, update);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error making first user admin");
                return false;
            }
        }
    }
}
