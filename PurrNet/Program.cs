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

// Add MariaDB — with retry for container cold-start / power outage recovery
var connectionString = Environment.GetEnvironmentVariable("MARIADB_CONNECTION_STRING") 
    ?? builder.Configuration.GetConnectionString("MariaDB") 
    ?? "Server=localhost;Database=PurrNet;User=purrnet;Password=purrnet;";

// Inside Docker, Server=localhost means the container itself, not the host.
// If user left localhost in .env, transparently rewrite to host.docker.internal so the host DB at 192.168.0.180 is reachable.
if (File.Exists("/.dockerenv") && connectionString.Contains("Server=localhost", StringComparison.OrdinalIgnoreCase))
{
    Console.WriteLine("[PurrNet] Detected Server=localhost inside container — rewriting to host.docker.internal (host DB at 192.168.0.180)");
    connectionString = connectionString.Replace("Server=localhost", "Server=host.docker.internal", StringComparison.OrdinalIgnoreCase)
                                       .Replace("Server=127.0.0.1", "Server=host.docker.internal", StringComparison.OrdinalIgnoreCase);
}
if (File.Exists("/.dockerenv") && connectionString.Contains("host.docker.internal", StringComparison.OrdinalIgnoreCase))
{
    Console.WriteLine($"[PurrNet] Using host DB via host.docker.internal: {connectionString.Split(';')[0]};...");
}

// HDD-slow / packet out-of-order fix: MariaDB on HDD + Pipelining = "Packet out-of-order. Expected 1; got 3."
// Pipelining=false prevents command interleaving (the root cause of packet errors).
// Pooling=true reuses TCP connections so every query doesn't pay the handshake cost on HDD (~1s each).
try
{
    var csb = new MySqlConnector.MySqlConnectionStringBuilder(connectionString);
    csb.Pipelining = false;
    csb.Pooling = true;
    csb.MinimumPoolSize = 1;
    csb.MaximumPoolSize = 10;
    csb.ConnectionIdleTimeout = 300; // 5 min — keep connections alive for reuse
    csb.DefaultCommandTimeout = 60;
    csb.AllowPublicKeyRetrieval = true;
    csb.SslMode = MySqlConnector.MySqlSslMode.None;
    connectionString = csb.ConnectionString;
}
catch (Exception ex)
{
    Console.WriteLine($"[PurrNet] MySqlConnectionStringBuilder rewrite failed, falling back to raw string: {ex.Message}");
    if (!connectionString.Contains("Pipelining", StringComparison.OrdinalIgnoreCase))
        connectionString += ";Pipelining=false";
}

Console.WriteLine($"[PurrNet] Final connecting string: {string.Join(";", connectionString.Split(';').Where(s => !s.ToLower().Contains("password") && !s.ToLower().Contains("pwd")))} (Pooling=true,Pipelining=false,cached)");

builder.Services.AddDbContext<PurrNetDbContext>(options =>
{
    // Fixed ServerVersion — never AutoDetect at startup (that opens a throwaway connection that can poison the pool)
    var serverVersion = ServerVersion.Parse("11.4.0-mariadb");

    options.UseMySql(connectionString, serverVersion, mySqlOpts =>
    {
        // No EnableRetryOnFailure — retrying a packet out-of-order just reuses the same bad pooled session
        mySqlOpts.CommandTimeout(60);
    });
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

// Dev vs published: dev (unpublished) runs on http://localhost without TLS — cookies must not require Secure there
var isPublished = builder.Environment.IsProduction();
var cookieSecure = isPublished ? CookieSecurePolicy.Always : CookieSecurePolicy.SameAsRequest;

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
    options.Cookie.SecurePolicy = cookieSecure;
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
    options.CorrelationCookie.SecurePolicy = cookieSecure;
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
    options.Cookie.SecurePolicy = cookieSecure;
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

// Seed default categories — background retry so power-outage / DB-late boots don't block startup (fixes systemd segfault on cold boot & keeps login page responsive)
var startupLifetime = app.Services.GetRequiredService<IHostApplicationLifetime>();
var connStrForAlter = connectionString; // captured before lambda for schema checks
startupLifetime.ApplicationStarted.Register(() =>
{
    _ = Task.Run(async () =>
    {
        // Brief delay to let MariaDB healthcheck pass in compose
        await Task.Delay(TimeSpan.FromSeconds(3));
        using var scope = app.Services.CreateScope();
        var startupLogger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("Startup");
        const int maxAttempts = 15;
        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<PurrNetDbContext>();
                await dbContext.Database.EnsureCreatedAsync();
                // Fixup for existing DBs — only run heavy ALTERs if actually needed (HDD slow + table lock → 5+ min review submit)
                async Task TryAlter(string sql, string msg)
                {
                    try { await dbContext.Database.ExecuteSqlRawAsync(sql); startupLogger.LogInformation(msg); }
                    catch (Exception alterEx) { startupLogger.LogDebug(alterEx, "ALTER skipped: {Sql}", sql); }
                }
                // Only ALTER PackageReviews longtext if not already longtext (check INFORMATION_SCHEMA to avoid 5-min COPY on HDD every boot)
                try
                {
                    // Use a separate connection for the schema check — never close EF Core's managed connection manually
                    using var checkConn = new MySqlConnector.MySqlConnection(connStrForAlter);
                    await checkConn.OpenAsync();
                    using var cmd = checkConn.CreateCommand();
                    cmd.CommandText = "SELECT DATA_TYPE FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'PackageReviews' AND COLUMN_NAME = 'ReviewerAvatarUrl'";
                    var dt = (await cmd.ExecuteScalarAsync())?.ToString();
                    // checkConn is disposed here, which closes it — EF Core's connection untouched
                    if (dt != null && !dt.Equals("longtext", StringComparison.OrdinalIgnoreCase))
                    {
                        await TryAlter("ALTER TABLE `PackageReviews` MODIFY COLUMN `ReviewerAvatarUrl` LONGTEXT NULL, MODIFY COLUMN `Title` LONGTEXT NULL, MODIFY COLUMN `Body` LONGTEXT NULL, MODIFY COLUMN `ReviewerName` VARCHAR(255) NULL", "Ensured PackageReviews columns are LONGTEXT");
                    }
                    else startupLogger.LogDebug("PackageReviews already LONGTEXT, skip ALTER");
                }
                catch (Exception ex) { startupLogger.LogDebug(ex, "Longtext check failed, attempting ALTER anyway"); await TryAlter("ALTER TABLE `PackageReviews` MODIFY COLUMN `ReviewerAvatarUrl` LONGTEXT NULL, MODIFY COLUMN `Title` LONGTEXT NULL, MODIFY COLUMN `Body` LONGTEXT NULL, MODIFY COLUMN `ReviewerName` VARCHAR(255) NULL", "Ensured PackageReviews columns are LONGTEXT"); }
                // IsLibrary — only if column doesn't exist yet
                await TryAlter("ALTER TABLE `Packages` ADD COLUMN `IsLibrary` INT NOT NULL DEFAULT 0", "Added Packages.IsLibrary");
                // Index for reviews — PackageReviews.PackageId was missing, causing full scan on HDD (5+ min on large table)
                await TryAlter("CREATE INDEX IF NOT EXISTS `IX_PackageReviews_PackageId` ON `PackageReviews` (`PackageId`)", "Ensured PackageReviews.PackageId index");
                await TryAlter("CREATE INDEX IF NOT EXISTS `IX_Packages_Name` ON `Packages` (`Name`)", "Ensured Packages.Name index");
                // AUTO_INCREMENT fix — only if column is NOT already auto_increment (expensive table rebuild on HDD)
                try
                {
                    using var ac = new MySqlConnector.MySqlConnection(connStrForAlter);
                    await ac.OpenAsync();
                    using var acmd = ac.CreateCommand();
                    acmd.CommandText = "SELECT EXTRA FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'Packages' AND COLUMN_NAME = 'Id'";
                    var extra = (await acmd.ExecuteScalarAsync())?.ToString();
                    if (extra == null || !extra.Contains("auto_increment", StringComparison.OrdinalIgnoreCase))
                    {
                        await TryAlter("ALTER TABLE `Packages` MODIFY COLUMN `Id` INT NOT NULL AUTO_INCREMENT", "Ensured Packages.Id AUTO_INCREMENT");
                        await TryAlter("ALTER TABLE `Users` MODIFY COLUMN `Id` INT NOT NULL AUTO_INCREMENT", "Ensured Users.Id AUTO_INCREMENT");
                        await TryAlter("ALTER TABLE `PackageReviews` MODIFY COLUMN `Id` INT NOT NULL AUTO_INCREMENT", "Ensured PackageReviews.Id AUTO_INCREMENT");
                    }
                    else startupLogger.LogDebug("Id columns already AUTO_INCREMENT, skip ALTER");
                }
                catch (Exception ex) { startupLogger.LogDebug(ex, "AUTO_INCREMENT check failed, attempting ALTER anyway"); await TryAlter("ALTER TABLE `Packages` MODIFY COLUMN `Id` INT NOT NULL AUTO_INCREMENT", "Ensured Packages.Id AUTO_INCREMENT"); }
                await dbContext.SeedDefaultCategoriesAsync();
                startupLogger.LogInformation("MariaDB ready (attempt {Attempt}/{Max})", attempt, maxAttempts);
                break;
            }
            catch (Exception ex)
            {
                if (attempt == maxAttempts)
                    startupLogger.LogError(ex, "MariaDB startup initialization failed after {Max} attempts — health will report degraded", maxAttempts);
                else
                {
                    startupLogger.LogWarning(ex, "MariaDB not ready (attempt {Attempt}/{Max}) — retrying in 4s...", attempt, maxAttempts);
                    await Task.Delay(TimeSpan.FromSeconds(4));
                }
            }
        }
    });
});

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

// Health check — actually probes DB so container HEALTHCHECK + load balancer see real readiness
app.MapGet("/health", async (PurrNetDbContext db) =>
{
    try
    {
        var canConnect = await db.Database.CanConnectAsync();
        return canConnect
            ? Results.Ok(new { status = "Healthy", database = "MariaDB" })
            : Results.Json(new { status = "Degraded", database = "unreachable" }, statusCode: 503);
    }
    catch (Exception ex)
    {
        return Results.Json(new { status = "Unhealthy", database = "error", detail = ex.Message }, statusCode: 503);
    }
});

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
