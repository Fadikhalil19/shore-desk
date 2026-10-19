using Desk.Api;
using Desk.Api.Auth;
using Desk.Api.Endpoints;
using Desk.Api.Services;
using Desk.Core.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.EntityFrameworkCore;
using Microsoft.Identity.Web;

// As a Windows Service the working folder is System32, so settings and web files
// must be found next to the program instead.
var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
    ContentRootPath = Microsoft.Extensions.Hosting.WindowsServices.WindowsServiceHelpers.IsWindowsService()
        ? AppContext.BaseDirectory : null,
});

// Runs as a Windows Service in production (no effect when started from a console).
builder.Host.UseWindowsService();

var testing = builder.Environment.IsEnvironment("Testing");
var appOptions = builder.Configuration.GetSection(AppOptions.Section).Get<AppOptions>() ?? new AppOptions();
builder.Services.Configure<AppOptions>(builder.Configuration.GetSection(AppOptions.Section));

builder.Services.AddDbContext<DeskDbContext>(o =>
    o.UseSqlServer(builder.Configuration.GetConnectionString("Desk")));

// Keys that encrypt the sign-in cookie. As a service they are kept in a folder only
// the service account can read (encrypted with its Windows credentials), so people
// stay signed in across restarts. In development the default per-user store is fine.
if (!builder.Environment.IsDevelopment() && !testing && OperatingSystem.IsWindows())
    builder.Services.AddDataProtection()
        .SetApplicationName("ShoreDesk")
        .PersistKeysToFileSystem(new DirectoryInfo(appOptions.KeysDir))
        .ProtectKeysWithDpapi();

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<CurrentUser>();
builder.Services.AddScoped<ActivityLogger>();
builder.Services.AddSingleton<FileStore>();
// Enums travel as their names ("HighLevel", "AwaitingReview"), not numbers.
builder.Services.ConfigureHttpJsonOptions(o =>
    o.SerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter()));
builder.Services.AddProblemDetails();

builder.Services.Configure<FormOptions>(o => o.MultipartBodyLengthLimit = appOptions.MaxUploadMb * 1024L * 1024L);
builder.WebHost.ConfigureKestrel(k => k.Limits.MaxRequestBodySize = appOptions.MaxUploadMb * 1024L * 1024L + 1024 * 1024);

// Sign-in: Microsoft Entra ID. The browser only holds an encrypted cookie; tokens stay on the server.
// (Automated tests replace sign-in with a test scheme, so they need no secret.)
var tenantId = builder.Configuration["AzureAd:TenantId"]!;
var clientSecret = testing ? "" : SecretFiles.Read(appOptions.SecretsDir, "entra-client-secret.txt");

builder.Services
    .AddAuthentication(OpenIdConnectDefaults.AuthenticationScheme)
    .AddMicrosoftIdentityWebApp(o =>
    {
        builder.Configuration.Bind("AzureAd", o);
        o.ClientSecret = clientSecret;
        // Authorization-code flow: tokens are fetched server-to-server with the secret,
        // so the "implicit grant" options in Entra stay switched off.
        o.ResponseType = Microsoft.IdentityModel.Protocols.OpenIdConnect.OpenIdConnectResponseType.Code;
    }, cookie =>
    {
        cookie.Cookie.Name = "desk";
        cookie.Cookie.HttpOnly = true;
        cookie.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        cookie.Cookie.SameSite = SameSiteMode.Lax;
        // A full shift plus overlap. Group changes take effect at the next sign-in;
        // an admin's Disable/Remove ends the session at once.
        cookie.ExpireTimeSpan = TimeSpan.FromHours(12);
        cookie.SlidingExpiration = false;
        cookie.Events.OnValidatePrincipal = SessionCheck.EndIfBlockedAsync;
        cookie.Events.OnRedirectToLogin = ctx => ApiAware(ctx, StatusCodes.Status401Unauthorized);
        cookie.Events.OnRedirectToAccessDenied = ctx => ApiAware(ctx, StatusCodes.Status403Forbidden);
    });

builder.Services.Configure<OpenIdConnectOptions>(OpenIdConnectDefaults.AuthenticationScheme, o =>
{
    // API calls get a plain 401 instead of a redirect to the Microsoft sign-in page.
    var previousRedirect = o.Events.OnRedirectToIdentityProvider;
    o.Events.OnRedirectToIdentityProvider = async ctx =>
    {
        if (ctx.Request.Path.StartsWithSegments("/api"))
        {
            ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
            ctx.HandleResponse();
            return;
        }
        await previousRedirect(ctx);
    };

    var previous = o.Events.OnTokenValidated;
    o.Events.OnTokenValidated = async ctx =>
    {
        await previous(ctx);
        await SignInHandler.OnTokenValidatedAsync(ctx, tenantId, appOptions);
    };
});

builder.Services.AddAuthorization(o =>
{
    // Default-deny: every endpoint needs a signed-in DESK member unless marked otherwise.
    // What they may see inside the app is then checked per module (RequireModule).
    o.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .RequireRole(nameof(AppRole.User), nameof(AppRole.Admin))
        .Build();
    o.AddPolicy(Policies.Admin, p => p.RequireRole(nameof(AppRole.Admin)));
});

var app = builder.Build();

if (builder.Configuration.GetValue<bool>("App:MigrateOnStartup"))
{
    using var scope = app.Services.CreateScope();
    scope.ServiceProvider.GetRequiredService<DeskDbContext>().Database.Migrate();
}

app.UseExceptionHandler();
app.UseHsts();
app.UseHttpsRedirection();
app.Use(async (ctx, next) =>
{
    var h = ctx.Response.Headers;
    h["X-Content-Type-Options"] = "nosniff";
    h["Referrer-Policy"] = "no-referrer";
    h["X-Frame-Options"] = "DENY";
    // Camera is allowed for this site only (visitor photos, barcode scanning in later phases).
    h["Permissions-Policy"] = "camera=(self), microphone=(), geolocation=()";
    h["Content-Security-Policy"] = "default-src 'self'; img-src 'self' data: blob:; style-src 'self' 'unsafe-inline'; frame-ancestors 'none'; base-uri 'self'; form-action 'self' https://login.microsoftonline.com";
    // Pages (HTML) are never cached, so after an update everyone gets the new screens.
    // The JS/CSS files have version numbers in their names, so they can be cached safely.
    ctx.Response.OnStarting(() =>
    {
        if (ctx.Response.ContentType?.StartsWith("text/html", StringComparison.OrdinalIgnoreCase) == true)
            ctx.Response.Headers.CacheControl = "no-cache, no-store, must-revalidate";
        return Task.CompletedTask;
    });
    await next();
});

// Built React files (JS/CSS/icons only, no data) are served before sign-in so the login redirect is fast.
app.UseStaticFiles();

app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<CsrfHeaderMiddleware>();

app.MapGet("/health", () => Results.Ok(new { status = "ok" })).AllowAnonymous();
app.MapAuthEndpoints();
app.MapMeEndpoints();
app.MapAdminEndpoints();
app.MapListEndpoints();
app.MapActivityEndpoints();
app.MapIncidentEndpoints();

// Unknown API addresses are a plain 404, never the web page.
app.MapFallback("/api/{**rest}", () => Results.NotFound());

// Everything else is the React app; the fallback policy makes it require sign-in.
app.MapFallbackToFile("index.html");

app.Run();

static Task ApiAware(RedirectContext<CookieAuthenticationOptions> ctx, int status)
{
    if (ctx.Request.Path.StartsWithSegments("/api"))
    {
        ctx.Response.StatusCode = status;
        return Task.CompletedTask;
    }
    ctx.Response.Redirect(ctx.RedirectUri);
    return Task.CompletedTask;
}

public static class Policies
{
    public const string Admin = "Admin";
}

public partial class Program;
