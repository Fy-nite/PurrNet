using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Purrnet.Services;
using System.Security.Claims;

namespace Purrnet.Pages.Account
{
    public class LoginModel : PageModel
    {
        private readonly IWebHostEnvironment _env;
        private readonly IUserService _userService;
        private readonly ILogger<LoginModel> _logger;
        private readonly IConfiguration _config;

        public string? ReturnUrl { get; set; }
        public bool AllowDevLogin { get; private set; }

        public LoginModel(IWebHostEnvironment env, IUserService userService, ILogger<LoginModel> logger, IConfiguration config)
        {
            _env = env;
            _userService = userService;
            _logger = logger;
            _config = config;
        }

        private bool IsDevLoginAllowed()
        {
            // Published = Production — never allow dev backdoor, even if config says otherwise
            if (_env.IsProduction())
                return false;

            // In dev/staging, config can explicitly disable (AllowDevLogin=false) without changing env
            var configured = _config["AllowDevLogin"];
            if (!string.IsNullOrWhiteSpace(configured) && bool.TryParse(configured, out var parsed))
                return parsed;

            // Default: only when NOT published (Development / Staging)
            // Published container sets ASPNETCORE_ENVIRONMENT=Production — dev login vanishes there
            return _env.IsDevelopment() || _env.IsStaging();
        }

        public void OnGet(string? returnUrl = null)
        {
            ReturnUrl = returnUrl ?? "/";
            AllowDevLogin = IsDevLoginAllowed();
        }

        public IActionResult OnPostAsync(string? returnUrl = null)
        {
            returnUrl ??= "/";
            AllowDevLogin = IsDevLoginAllowed();

            return Challenge(new AuthenticationProperties
            {
                RedirectUri = returnUrl
            }, "GitHub");
        }

        // Dev-only local admin — never available when published (Production)
        public async Task<IActionResult> OnPostDevAsync(string? returnUrl = null)
        {
            returnUrl ??= "/";
            AllowDevLogin = IsDevLoginAllowed();

            if (!AllowDevLogin)
            {
                _logger.LogWarning("Blocked dev login attempt outside dev mode (env={Env}, ip={Ip})", _env.EnvironmentName, HttpContext.Connection.RemoteIpAddress);
                return NotFound();
            }

            // Synthetic GitHubId that never collides with real GitHub (real IDs are >0)
            const string devGitHubId = "0";
            const string devUsername = "Dev";
            const string devEmail = "dev@localhost";

            var user = await _userService.GetUserByGitHubIdAsync(devGitHubId);
            if (user == null)
            {
                // Also check by username in case old dev row used a different id
                user = await _userService.GetUserByUsernameAsync(devUsername);
            }

            if (user == null)
            {
                _logger.LogInformation("Creating dev local admin user '{Username}'", devUsername);
                user = await _userService.CreateUserAsync(devGitHubId, devUsername, devEmail, "");
                // Ensure admin regardless of CreateUser default
                if (user.IsAdmin != 1)
                {
                    await _userService.PromoteToAdminAsync(user.Id.ToString());
                    user.IsAdmin = 1;
                }
            }
            else
            {
                // Ensure existing dev account is admin and username is normalized
                if (user.IsAdmin != 1)
                {
                    await _userService.PromoteToAdminAsync(user.Id.ToString());
                    user.IsAdmin = 1;
                }
                // Touch last login
                await _userService.UpdateUserAsync(user);
            }

            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, user.GitHubId.ToString()),
                new(ClaimTypes.Name, user.Username),
                new(ClaimTypes.Email, user.Email ?? devEmail),
                new("urn:github:login", user.Username),
                new("UserId", user.Id.ToString()),
                new("IsAdmin", user.IsAdmin.ToString()),
                // compatibility: some views check "True"
                new("IsAdmin", user.IsAdmin == 1 ? "True" : "False"),
            };

            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var principal = new ClaimsPrincipal(identity);

            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal, new AuthenticationProperties
            {
                IsPersistent = true,
                ExpiresUtc = DateTimeOffset.UtcNow.AddHours(24),
                RedirectUri = returnUrl
            });

            _logger.LogInformation("Dev login signed in '{Username}' (Id={Id}, IsAdmin={Admin}) -> {ReturnUrl}", user.Username, user.Id, user.IsAdmin, returnUrl);
            return LocalRedirect(returnUrl);
        }
    }
}
