using Microsoft.EntityFrameworkCore;
using Purrnet.Data;
using Purrnet.Models;

namespace Purrnet.Services
{
    public class UserService : IUserService
    {
        private readonly PurrDbContext _context;
        private readonly ILogger<UserService> _logger;

        public UserService(PurrDbContext context, ILogger<UserService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<User?> GetUserByGitHubIdAsync(string gitHubId)
        {
            try
            {
                return await _context.Users.FirstOrDefaultAsync(u => u.GitHubId == gitHubId);
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

                _context.Users.Add(user);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Created new user {Username} with GitHub ID {GitHubId}", username, gitHubId);
                return user;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating user {Username} with GitHub ID {GitHubId}", username, gitHubId);
                throw;
            }
        }

        public async Task<User> UpdateUserAsync(User user)
        {
            try
            {
                user.LastLoginAt = DateTime.UtcNow;
                _context.Users.Update(user);
                await _context.SaveChangesAsync();
                return user;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating user {Username}", user.Username);
                throw;
            }
        }

        public async Task<bool> IsAdminAsync(int userId)
        {
            try
            {
                var user = await _context.Users.FindAsync(userId);
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
                return await _context.Users.OrderBy(u => u.Username).ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving all users");
                return new List<User>();
            }
        }

        public async Task<bool> PromoteToAdminAsync(int userId)
        {
            try
            {
                var user = await _context.Users.FindAsync(userId);
                if (user != null)
                {
                    user.IsAdmin = true;
                    await _context.SaveChangesAsync();
                    _logger.LogInformation("User {Username} promoted to admin", user.Username);
                    return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error promoting user {UserId} to admin", userId);
                return false;
            }
        }

        public async Task<bool> RevokeAdminAsync(int userId)
        {
            try
            {
                var user = await _context.Users.FindAsync(userId);
                if (user != null)
                {
                    user.IsAdmin = false;
                    await _context.SaveChangesAsync();
                    _logger.LogInformation("Admin privileges revoked for user {Username}", user.Username);
                    return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error revoking admin privileges for user {UserId}", userId);
                return false;
            }
        }

        public async Task<bool> BanUserAsync(int userId)
        {
            try
            {
                var user = await _context.Users.FindAsync(userId);
                if (user != null)
                {
                    user.IsBanned = true;
                    await _context.SaveChangesAsync();
                    _logger.LogInformation("User {Username} (ID {UserId}) banned", user.Username, userId);
                    return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error banning user {UserId}", userId);
                return false;
            }
        }

        public async Task<bool> UnbanUserAsync(int userId)
        {
            try
            {
                var user = await _context.Users.FindAsync(userId);
                if (user != null)
                {
                    user.IsBanned = false;
                    await _context.SaveChangesAsync();
                    _logger.LogInformation("User {Username} (ID {UserId}) unbanned", user.Username, userId);
                    return true;
                }
                return false;
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
                return await _context.Users.FirstOrDefaultAsync(u => u.Username == username);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving user by username {Username}", username);
                return null;
            }
        }

        public async Task<List<Package>> GetUserPackagesAsync(int userId)
        {
            try
            {
                return await _context.Packages
                    .Where(p => p.OwnerId == userId && p.IsActive)
                    .OrderByDescending(p => p.CreatedAt)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting packages for user {UserId}", userId);
                return new List<Package>();
            }
        }

        public async Task<List<Package>> GetUserMaintainedPackagesAsync(int userId)
        {
            try
            {
                // For now, return packages where user is listed as maintainer
                // This could be expanded with a proper many-to-many relationship
                return await _context.Packages
                    .Where(p => p.IsActive && p.Maintainers.Any(m => m.Id == userId))
                    .OrderByDescending(p => p.LastUpdated)
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
                var userCount = await _context.Users.CountAsync();
                if (userCount == 1)
                {
                    var firstUser = await _context.Users.FirstAsync();
                    firstUser.IsAdmin = true;
                    await _context.SaveChangesAsync();
                    return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error making first user admin");
                return false;
            }
        }
    }
}
