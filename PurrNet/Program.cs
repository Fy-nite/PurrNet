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
using Microsoft.Extensions.Primitives; 
using Purrnet.Models;

// Load environment variables from .env file
DotNetEnv.Env.TraversePath().Load();

var builder = WebApplication.CreateBuilder(args);

// Handle admin CLI commands before starting the web server
if (args.Length > 0 && args[0] == "--admin")
{
    var exitCode = await AdminCommand.ExecuteAsync(args);
    Environment.Exit(exitCode);
}

// Add services to the container.
builder.Services.AddRazorPages();
builder.Services.AddControllers();

// Base path configuration for reverse proxy support
var basePath = builder.Configuration.GetValue<string>("BasePath") ?? "/";
var trustForwardHeaders = builder.Configuration.GetValue<bool>("TrustForwardHeaders", true);

// Add MariaDB
var connectionString = Environment.GetEnvironmentVariable("MARIADB_CONNECTION_STRING") ?? builder.Configuration.GetConnectionString("MariaDB") ?? "Server=localhost;Database=PurrNet;User=purrnet;Password=purrnet;";
builder.Services.AddDbContext<PurrNetDbContext>(options =>
{
    options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString));
});

// Configure Forwarded Headers for proxy
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
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
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
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
                Secure = true,
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
    options.CorrelationCookie.SecurePolicy = CookieSecurePolicy.Always;
    options.CorrelationCookie.HttpOnly = true;
    options.CorrelationCookie.Expiration = TimeSpan.FromMinutes(15);
    
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
        OnRemoteFailure = context =>
        {
            var logger = context.HttpContext.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("OAuth");
            logger.LogError("Remote authentication failure: {Error}. State: {State}", context.Failure?.Message, context.Request.Query["state"]);
            
            // Log details about the request to help debug
            logger.LogDebug("Failure Request Path: {Path}, Scheme: {Scheme}, Host: {Host}", 
                context.Request.Path, context.Request.Scheme, context.Request.Host);
                
            return Task.CompletedTask;
        },
        OnRedirectToAuthorizationEndpoint = context =>
        {
            var logger = context.HttpContext.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("OAuth");
            
            // Log the redirect URI being sent to GitHub
            logger.LogInformation("Redirecting to GitHub Authorization. Final URL: {RedirectUri}", context.RedirectUri);
            
            // Check if the internal redirect_uri parameter is using HTTP instead of HTTPS
            if (context.RedirectUri.Contains("redirect_uri=http%3A"))
            {
                logger.LogWarning("Detected HTTP redirect_uri in authorization request! Forcing to HTTPS.");
                context.RedirectUri = context.RedirectUri.Replace("redirect_uri=http%3A", "redirect_uri=https%3A");
                logger.LogInformation("Corrected URL: {RedirectUri}", context.RedirectUri);
            }
            
            context.Response.Redirect(context.RedirectUri);
            return Task.CompletedTask;
        },
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
            var oauthLogger = context.HttpContext.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("OAuth");
            
            string gitHubId;
            try 
            {
                gitHubId = json.RootElement.GetProperty("id").ToString();
                oauthLogger.LogInformation("Processing GitHub login for user {Login} (GitHub ID: {GitHubId})", 
                    json.RootElement.GetProperty("login").GetString(), gitHubId);
            }
            catch (Exception ex)
            {
                oauthLogger.LogError(ex, "Failed to parse GitHub user information from JSON response");
                return;
            }

            var username = json.RootElement.GetProperty("login").GetString() ?? "";
            var email = json.RootElement.TryGetProperty("email", out var emailProp) ? emailProp.GetString() ?? "" : "";
            var avatarUrl = json.RootElement.GetProperty("avatar_url").GetString() ?? "";
            
            try
            {
                oauthLogger.LogDebug("Looking up user {Username} by GitHub ID {GitHubId}...", username, gitHubId);
                var user = await userService.GetUserByGitHubIdAsync(gitHubId);
                
                if (user == null)
                {
                    oauthLogger.LogInformation("User {Username} not found in database, creating new record...", username);
                    user = await userService.CreateUserAsync(gitHubId, username, email, avatarUrl);
                    oauthLogger.LogInformation("Created new user {Username} with internal ID {UserId}", username, user.Id);
                }
                else
                {
                    oauthLogger.LogInformation("User {Username} found in database (internal ID: {UserId}), updating profile...", username, user.Id);
                    user = await userService.UpdateUserAsync(user);
                }

                // Add custom claims to the identity
                var identity = (ClaimsIdentity)context.Principal!.Identity!;
                identity.AddClaim(new Claim("UserId", user.Id.ToString()));
                identity.AddClaim(new Claim("IsAdmin", user.IsAdmin.ToString()));
                
                oauthLogger.LogInformation("Successfully added claims for {Username}. UserId: {UserId}, IsAdmin: {IsAdmin}", 
                    username, user.Id, user.IsAdmin);
            }
            catch (Exception ex)
            {
                oauthLogger.LogError(ex, "Database error while persisting user {Username} during login", username);
            }
        }
    };
});

// Add memory cache
builder.Services.AddMemoryCache();

// Add session support
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
});

// Add TestingModeService
bool isTestingMode = builder.Configuration.GetValue<bool>("TestingMode", false);
builder.Services.AddSingleton(new TestingModeService(isTestingMode));

var app = builder.Build();

// Enable Forwarded Headers immediately
app.UseForwardedHeaders();

// Request logging for debugging
app.Use(async (context, next) =>
{
    var logger = context.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("RequestLogging");
    logger.LogInformation("Request: {Method} {Path}{Query} (Scheme: {Scheme})", 
        context.Request.Method, context.Request.Path, context.Request.QueryString, context.Request.Scheme);
    await next();
});

app.UsePathBase(basePath); // Set the base path for the application
Console.WriteLine($"Application base path set to {basePath}");

// If configured, trust common proxy forwarded headers and allow the proxy to set a request PathBase
if (trustForwardHeaders)
{
    var loggerFactory = app.Services.GetRequiredService<ILoggerFactory>();
    var fwdLogger = loggerFactory.CreateLogger("ForwardedHeaders");
    fwdLogger.LogInformation("TRUST_FORWARD_HEADERS=true — enabling forwarding of headers and X-Forwarded-Prefix support (trusted proxy)");

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

// Seed default categories on startup
using (var scope = app.Services.CreateScope())
{
    var startupLogger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("Startup");
    var dbContext = scope.ServiceProvider.GetRequiredService<PurrNetDbContext>();
    try
    {
        await dbContext.SeedDefaultCategoriesAsync();
        startupLogger.LogInformation("MariaDB ready");
    }
    catch (Exception ex)
    {
        startupLogger.LogError(ex, "MariaDB startup initialization failed");
    }
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseStaticFiles();

app.UseRouting();

app.UseSession();
app.UseAuthentication();
app.UseAuthorization();

app.MapRazorPages();
app.MapControllers();

// Health check endpoint
app.MapGet("/health", () => Results.Ok(new { status = "Healthy", database = "MariaDB" }));

// Minimal version info API for the CLI
app.MapGet("/api/version", async (IConfiguration config) =>
{
    var version = "1.0.0";
    try
    {
        using var client = new HttpClient();
        client.DefaultRequestHeaders.Add("User-Agent", "PurrNet-Server");
        var response = await client.GetAsync("https://api.github.com/repos/Finite-Finite/PurrNet/releases/latest");
        var json = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json);
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
