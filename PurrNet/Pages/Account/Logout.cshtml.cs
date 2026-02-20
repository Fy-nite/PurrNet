using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Purrnet.Pages.Account
{
    public class LogoutModel : PageModel
    {
        private readonly ILogger<LogoutModel> _logger;

        public LogoutModel(ILogger<LogoutModel> logger)
        {
            _logger = logger;
        }

        public async Task<IActionResult> OnGetAsync()
        {
            return await LogoutAsync();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            return await LogoutAsync();
        }

        private async Task<IActionResult> LogoutAsync()
        {
            try
            {
                var userName = User.Identity?.Name ?? "anonymous";
                _logger.LogInformation("Starting logout for user: {UserName}", userName);
                
                // Clear session first
                if (HttpContext.Session.IsAvailable)
                {
                    HttpContext.Session.Clear();
                }
                
                // Sign out from cookie authentication scheme
                await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                
                // Delete authentication cookies explicitly
                var cookiesToDelete = new[]
                {
                    ".AspNetCore.PurrNet.Auth",
                    ".AspNetCore.PurrNet.Correlation", 
                    ".AspNetCore.Antiforgery",
                    ".AspNetCore.Session"
                };

                foreach (var cookieName in cookiesToDelete)
                {
                    Response.Cookies.Delete(cookieName, new CookieOptions
                    {
                        Path = Request.PathBase.HasValue ? Request.PathBase.ToString() : "/",
                        HttpOnly = true,
                        Secure = Request.IsHttps,
                        SameSite = SameSiteMode.Lax
                    });
                }
                
                // Set cache control headers
                Response.Headers.Append("Cache-Control", "no-cache, no-store, must-revalidate");
                Response.Headers.Append("Pragma", "no-cache");
                Response.Headers.Append("Expires", "0");
                
                _logger.LogInformation("Logout completed for user: {UserName}", userName);
                
                // Redirect to configured PathBase-aware home page
                var home = Request.PathBase.HasValue ? Request.PathBase.ToString() + "/" : "/";
                return LocalRedirect(home);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during logout");
                var home = Request.PathBase.HasValue ? Request.PathBase.ToString() + "/" : "/";
                return LocalRedirect(home);
            }
        }
    }
}
