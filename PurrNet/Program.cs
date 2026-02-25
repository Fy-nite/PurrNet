using Microsoft.EntityFrameworkCore;
using Purrnet.Data;
using Purrnet.Services;
using Microsoft.AspNetCore.Authentication.OAuth;
using Microsoft.AspNetCore.Authentication.Cookies;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using DotNetEnv;
using Microsoft.AspNetCore.HttpOverrides;
using Purrnet.Commands;
using Microsoft.Extensions.Primitives; // Add this for AdminCommand

// Handle admin CLI commands before starting the web server
if (args.Length > 0 && args[0] == "--admin")
{
    var exitCode = await AdminCommand.ExecuteAsync(args);
    return exitCode;
}

// Load environment variables from .env file
Env.Load();
string version = "1.2.0";

var builder = WebApplication.CreateBuilder(args);
var basePath = Environment.GetEnvironmentVariable("BASE_PATH") ?? builder.Configuration["BasePath"] ?? "/purr";
var trustForwardHeaders = (Environment.GetEnvironmentVariable("TRUST_FORWARD_HEADERS") ?? builder.Configuration["TrustForwardHeaders"])?.ToLower() == "true";

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
    options.Cookie.Path = basePath;
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
        
        var deletePath = context.HttpContext.Request.PathBase.HasValue ? context.HttpContext.Request.PathBase.ToString() : "/";
        foreach (var cookieName in cookiesToClear)
        {
            context.Response.Cookies.Delete(cookieName, new CookieOptions
            {
                Path = deletePath,
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
    options.CorrelationCookie.Path = basePath;
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
app.UsePathBase(basePath); // Set the base path for the application
Console.WriteLine($"Application base path set to {basePath}");
// If configured, trust common proxy forwarded headers and allow the proxy to set a request PathBase
if (trustForwardHeaders)
{
    var loggerFactory = app.Services.GetRequiredService<ILoggerFactory>();
    var fwdLogger = loggerFactory.CreateLogger("ForwardedHeaders");
    fwdLogger.LogInformation("TRUST_FORWARD_HEADERS=true — enabling forwarding of headers and X-Forwarded-Prefix support (trusted proxy)");

    var forwardOptions = new ForwardedHeadersOptions
    {
        ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost
    };
    // Allow forwarded headers from any proxy (operator must ensure this is safe in their environment)
    forwardOptions.KnownNetworks.Clear();
    forwardOptions.KnownProxies.Clear();

    app.UseForwardedHeaders(forwardOptions);

    app.Use(async (context, next) =>
    {
        // Prefer X-Forwarded-Prefix (commonly used), then X-Forwarded-Path, then X-Original-URI
        string? prefix = null;
        if (context.Request.Headers.TryGetValue("X-Forwarded-Prefix", out var v) && !StringValues.IsNullOrEmpty(v)) prefix = v.ToString();
        else if (context.Request.Headers.TryGetValue("X-Forwarded-Path", out var v2) && !StringValues.IsNullOrEmpty(v2)) prefix = v2.ToString();
        else if (context.Request.Headers.TryGetValue("X-Original-URI", out var v3) && !StringValues.IsNullOrEmpty(v3)) prefix = v3.ToString();

        if (!string.IsNullOrEmpty(prefix))
        {
            // Trim query and ensure leading slash
            var cleaned = prefix.Split('?')[0];
            if (!cleaned.StartsWith('/')) cleaned = "/" + cleaned;
            try
            {
                context.Request.PathBase = new PathString(cleaned);
                fwdLogger.LogDebug("Applied forwarded prefix '{Prefix}' to Request.PathBase", cleaned);
            }
            catch (Exception ex)
            {
                fwdLogger.LogWarning(ex, "Invalid forwarded prefix '{Prefix}', ignoring", prefix);
            }
        }

        await next();
    });
}
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
var contentTypeProvider = new Microsoft.AspNetCore.StaticFiles.FileExtensionContentTypeProvider();
contentTypeProvider.Mappings[".ps1"] = "text/plain";
contentTypeProvider.Mappings[".sh"] = "text/plain";
app.UseStaticFiles(new StaticFileOptions
{
    ContentTypeProvider = contentTypeProvider
});



app.UseRouting();
// Enable CORS for API
app.UseCors("ApiPolicy");

// Add session middleware before authentication
app.UseSession();
app.UseAuthentication();
app.UseAuthorization();

app.MapRazorPages();
app.MapControllers();

// Simple endpoint to return the latest release tag as plain text for installers
app.MapGet("/Latest", async () =>
{
    try
    {
        using var httpClient = new HttpClient();
        httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("PurrInstaller/1.0");
        var response = await httpClient.GetStringAsync("https://api.github.com/repos/Fy-nite/Purr/releases/latest");
        using var doc = System.Text.Json.JsonDocument.Parse(response);
        var tag = doc.RootElement.GetProperty("tag_name").GetString();
        return Results.Text(tag?.TrimStart('v') ?? "Unknown");
    }
    catch
    {
        return Results.Text(version);
    }
});

app.Run();

// Ensure the program returns 0 for success
return 0;
