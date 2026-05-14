using Microsoft.EntityFrameworkCore;
using Purrnet.Data;
using Purrnet.Models;

namespace Purrnet.Services
{
    public class UserService : IUserService
    {
        private readonly PurrNetDbContext _context;
        private readonly ILogger<UserService> _logger;

        public UserService(PurrNetDbContext context, ILogger<UserService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<User?> GetUserByGitHubIdAsync(string gitHubId)
        {
            if (int.TryParse(gitHubId, out int id))
            {
                return await _context.Users.FirstOrDefaultAsync(u => u.GitHubId == id);
            }
            return null;
        }

        public async Task<User> CreateUserAsync(string gitHubId, string username, string email, string avatarUrl)
        {
            var user = new User
            {
                GitHubId = int.Parse(gitHubId),
                Username = username,
                Email = email,
                AvatarUrl = avatarUrl,
                CreatedAt = DateTime.UtcNow.ToString("O"),
                LastLoginAt = DateTime.UtcNow.ToString("O"),
                IsAdmin = 0
            };
            _context.Users.Add(user);
            await _context.SaveChangesAsync();
            return user;
        }

        public async Task<User> UpdateUserAsync(User user)
        {
            user.LastLoginAt = DateTime.UtcNow.ToString("O");
            _context.Users.Update(user);
            await _context.SaveChangesAsync();
            return user;
        }

        public async Task<bool> IsAdminAsync(string userId)
        {
            var user = await _context.Users.FindAsync(int.Parse(userId));
            return user?.IsAdmin == 1;
        }

        public async Task<List<User>> GetAllUsersAsync() => await _context.Users.ToListAsync();

        public async Task<bool> PromoteToAdminAsync(string userId)
        {
            var user = await _context.Users.FindAsync(int.Parse(userId));
            if (user == null) return false;
            user.IsAdmin = 1;
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> RevokeAdminAsync(string userId)
        {
            var user = await _context.Users.FindAsync(int.Parse(userId));
            if (user == null) return false;
            user.IsAdmin = 0;
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> BanUserAsync(string userId)
        {
            var user = await _context.Users.FindAsync(int.Parse(userId));
            if (user == null) return false;
            user.IsBanned = 1;
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> UnbanUserAsync(string userId)
        {
            var user = await _context.Users.FindAsync(int.Parse(userId));
            if (user == null) return false;
            user.IsBanned = 0;
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<User?> GetUserByUsernameAsync(string username) => await _context.Users.FirstOrDefaultAsync(u => u.Username == username);

        public async Task<List<Package>> GetUserPackagesAsync(string userId) => await _context.Packages.Where(p => p.OwnerId == int.Parse(userId)).ToListAsync();

        public async Task<List<Package>> GetUserMaintainedPackagesAsync(string userId) => new List<Package>();

        public async Task<bool> MakeFirstUserAdminAsync()
        {
            var first = await _context.Users.FirstOrDefaultAsync();
            if (first == null) return false;
            first.IsAdmin = 1;
            await _context.SaveChangesAsync();
            return true;
        }
    }
}
