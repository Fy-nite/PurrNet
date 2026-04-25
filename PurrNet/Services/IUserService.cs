using Purrnet.Models;

namespace Purrnet.Services
{
    public interface IUserService
    {
        Task<User?> GetUserByGitHubIdAsync(string gitHubId);
        Task<User?> GetUserByUsernameAsync(string username);
        Task<User> CreateUserAsync(string gitHubId, string username, string email, string avatarUrl);
        Task<User> UpdateUserAsync(User user);
        Task<bool> IsAdminAsync(string userId);
        Task<List<Package>> GetUserPackagesAsync(string userId);
        Task<List<Package>> GetUserMaintainedPackagesAsync(string userId);
        Task<List<User>> GetAllUsersAsync();
        Task<bool> PromoteToAdminAsync(string userId);
        Task<bool> RevokeAdminAsync(string userId);
        Task<bool> MakeFirstUserAdminAsync();
        Task<bool> BanUserAsync(string userId);
        Task<bool> UnbanUserAsync(string userId);
    }
}
