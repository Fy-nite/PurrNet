using Microsoft.EntityFrameworkCore;
using Purrnet.Data;
using Purrnet.Services;
using Microsoft.AspNetCore.Authentication.OAuth;
using Microsoft.AspNetCore.Authentication.Cookies;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using DotNetEnv;
using Purrnet.Commands; // Add this for AdminCommand

// Handle admin CLI commands before starting the web server
if (args.Length > 0 && args[0] == "--admin")
{
    var exitCode = await AdminCommand.ExecuteAsync(args);
    return exitCode;
}

// Load environment variables from .env file
Env.Load();

var builder = WebApplication.CreateBuilder(args);

// Check for testing/debug mode from command line arguments
var isTestingMode = args.Contains("--test") || args.Contains("--debug");
Console.WriteLine($"Testing mode: {isTestingMode}");
Console.WriteLine($"Command line args: {string.Join(", ", args)}");

builder.Services.AddSingleton(new TestingModeService(isTestingMode));

// Add services to the container.
builder.Services.AddRazorPages();
builder.Services.AddControllers();

// Add CORS for API access
builder.Services.AddCors(options =>
{
    options.AddPolicy("ApiPolicy", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// Add API documentation
builder.Services.AddEndpointsApiExplorer();

// Add Entity Framework
builder.Services.AddDbContext<PurrDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

// Add memory cache
builder.Services.AddMemoryCache();

// Add session support for better logout handling
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

// Register services
builder.Services.AddScoped<IPackageService, PackageService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IAdminService, AdminService>();

// Configure authentication
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    options.DefaultSignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = "GitHub";
})
.AddCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.LogoutPath = "/Account/Logout";
    options.ExpireTimeSpan = TimeSpan.FromHours(24);
    options.SlidingExpiration = true;
    options.Cookie.Name = ".AspNetCore.PurrNet.Auth";
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Events.OnSigningOut = async context =>
    {
        // Clear session during sign out
        if (context.HttpContext.Session.IsAvailable)
        {
            context.HttpContext.Session.Clear();
        }
        
        // Force clear all auth cookies
        var cookiesToClear = new[] 
        {
            ".AspNetCore.PurrNet.Auth.",
            ".AspNetCore.PurrNet.Correlation.",
            ".AspNetCore.Antiforgery.",
            ".AspNetCore.Session."
        };
        
        foreach (var cookieName in cookiesToClear)
        {
            context.Response.Cookies.Delete(cookieName, new CookieOptions
            {
                Path = "/",
                HttpOnly = true,
                Secure = context.HttpContext.Request.IsHttps,
                SameSite = SameSiteMode.Lax
            });
        }
        
        Console.WriteLine($"Cookie authentication sign out completed for user: {context.HttpContext.User?.Identity?.Name}");
    };
})
.AddOAuth("GitHub", options =>
{
    options.ClientId = Environment.GetEnvironmentVariable("GITHUB_CLIENT_ID") ?? builder.Configuration["GitHub:ClientId"] ?? "";
    options.ClientSecret = Environment.GetEnvironmentVariable("GITHUB_CLIENT_SECRET") ?? builder.Configuration["GitHub:ClientSecret"] ?? "";
    options.CallbackPath = new PathString("/signin-github");
    
    // Fix correlation issues with custom domain
    options.CorrelationCookie.Name = ".AspNetCore.PurrNet.Correlation";
    options.CorrelationCookie.SameSite = SameSiteMode.Lax;
    options.CorrelationCookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    options.CorrelationCookie.HttpOnly = true;
    options.CorrelationCookie.Expiration = TimeSpan.FromMinutes(5); // Short expiration
    
    options.AuthorizationEndpoint = "https://github.com/login/oauth/authorize";
    options.TokenEndpoint = "https://github.com/login/oauth/access_token";
    options.UserInformationEndpoint = "https://api.github.com/user";
    
    options.Scope.Add("user:email");
    
    options.ClaimActions.MapJsonKey(ClaimTypes.NameIdentifier, "id");
    options.ClaimActions.MapJsonKey(ClaimTypes.Name, "name");
    options.ClaimActions.MapJsonKey(ClaimTypes.Email, "email");
    options.ClaimActions.MapJsonKey("urn:github:login", "login");
    options.ClaimActions.MapJsonKey("urn:github:url", "html_url");
    options.ClaimActions.MapJsonKey("urn:github:avatar", "avatar_url");
    
    options.Events = new OAuthEvents
    {
        OnCreatingTicket = async context =>
        {
            var request = new HttpRequestMessage(HttpMethod.Get, context.Options.UserInformationEndpoint);
            request.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", context.AccessToken);

            var response = await context.Backchannel.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, context.HttpContext.RequestAborted);
            response.EnsureSuccessStatusCode();

            var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            context.RunClaimActions(json.RootElement);
            
            // Store user info in database
            var userService = context.HttpContext.RequestServices.GetRequiredService<IUserService>();
            var gitHubId = json.RootElement.GetProperty("id").GetInt32().ToString();
            var username = json.RootElement.GetProperty("login").GetString() ?? "";
            var email = json.RootElement.TryGetProperty("email", out var emailProp) ? emailProp.GetString() ?? "" : "";
            var avatarUrl = json.RootElement.GetProperty("avatar_url").GetString() ?? "";
            
            try
            {
                var user = await userService.GetUserByGitHubIdAsync(gitHubId);
                if (user == null)
                {
                    user = await userService.CreateUserAsync(gitHubId, username, email, avatarUrl);
                }
                else
                {
                    user = await userService.UpdateUserAsync(user);
                }

                // Add custom claims
                var identity = (ClaimsIdentity)context.Principal!.Identity!;
                identity.AddClaim(new Claim("UserId", user.Id.ToString()));
                identity.AddClaim(new Claim("IsAdmin", user.IsAdmin.ToString()));
            }
            catch (Exception ex)
            {
                var oauthLogger = context.HttpContext.RequestServices
                    .GetRequiredService<ILoggerFactory>().CreateLogger("OAuth");
                oauthLogger.LogError(ex, "Failed to persist user {Username} during GitHub OAuth login; user claims will be omitted", username);
            }
        }
    };
});

var app = builder.Build();

// Apply migrations automatically on startup
using (var scope = app.Services.CreateScope())
{
    var startupLogger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("Startup");
    var context = scope.ServiceProvider.GetRequiredService<PurrDbContext>();
    try
    {
        context.Database.Migrate();
        startupLogger.LogInformation("Database migrations applied successfully");
    }
    catch (Exception ex)
    {
        startupLogger.LogError(ex, "Migration failed; attempting EnsureCreated as fallback");
        try
        {
            context.Database.EnsureCreated();
            startupLogger.LogInformation("Database schema created via EnsureCreated fallback");
        }
        catch (Exception innerEx)
        {
            startupLogger.LogCritical(innerEx, "Database initialization failed entirely — the app may not function correctly");
        }
    }
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

// Comment out this line temporarily for HTTP testing
// app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

// Enable CORS for API
app.UseCors("ApiPolicy");

// Add session middleware before authentication
app.UseSession();
app.UseAuthentication();
app.UseAuthorization();

app.MapRazorPages();
app.MapControllers();

app.Run();

// Ensure the program returns 0 for success
return 0;
