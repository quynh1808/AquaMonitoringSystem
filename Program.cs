using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Antiforgery;
using System.Threading.RateLimiting;
using AquaAuthenticationOptions = AquacultureMonitoring.Configuration.AuthenticationOptions;
using System.Security.Claims;
using Microsoft.Extensions.Options;
using AquacultureMonitoring.Components;
using AquacultureMonitoring.Configuration;
using AquacultureMonitoring.Data;
using AquacultureMonitoring.Devices;
using AquacultureMonitoring.Infrastructure;
using AquacultureMonitoring.Models;
using AquacultureMonitoring.Runtime;
using AquacultureMonitoring.Sensors;
using AquacultureMonitoring.Services;

const string resetAdminPasswordCommand = "reset-admin-password";
var resetAdminPasswordMode = args.Contains(resetAdminPasswordCommand, StringComparer.OrdinalIgnoreCase);
var hostArguments = args.Where(x => !x.Equals(resetAdminPasswordCommand, StringComparison.OrdinalIgnoreCase)).ToArray();
var builder = WebApplication.CreateBuilder(hostArguments);

builder.Configuration
    .AddJsonFile("Config/sensor-thresholds.json", optional: false, reloadOnChange: true)
    .AddJsonFile("Config/simulation-profile.json", optional: false, reloadOnChange: true)
    .AddJsonFile("Config/sensor-connections.json", optional: false, reloadOnChange: true)
    .AddJsonFile("Config/system-behavior.json", optional: false, reloadOnChange: true)
    .AddJsonFile("Config/dashboard-simulation.json", optional: false, reloadOnChange: true)
    .AddJsonFile("Config/authentication.json", optional: false, reloadOnChange: true)
    .AddJsonFile("Config/device-control.json", optional: false, reloadOnChange: true)
    .AddJsonFile("Config/production-cycle-simulation.json", optional: false, reloadOnChange: true);

builder.Services
    .AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddCascadingAuthenticationState();

builder.Services.AddOptions<MongoDbOptions>()
    .Bind(builder.Configuration.GetSection(MongoDbOptions.SectionName)).ValidateOnStart();
builder.Services.AddOptions<SensorThresholdCatalog>()
    .Bind(builder.Configuration.GetSection(SensorThresholdCatalog.SectionName)).ValidateOnStart();
builder.Services.AddOptions<SimulationProfileOptions>()
    .Bind(builder.Configuration.GetSection(SimulationProfileOptions.SectionName)).ValidateOnStart();
builder.Services.AddOptions<SensorConnectionOptions>()
    .Bind(builder.Configuration.GetSection(SensorConnectionOptions.SectionName)).ValidateOnStart();
builder.Services.AddOptions<SystemBehaviorOptions>()
    .Bind(builder.Configuration.GetSection(SystemBehaviorOptions.SectionName)).ValidateOnStart();
builder.Services.AddOptions<DashboardSimulationOptions>()
    .Bind(builder.Configuration.GetSection(DashboardSimulationOptions.SectionName)).ValidateOnStart();
builder.Services.AddOptions<AquaAuthenticationOptions>()
    .Bind(builder.Configuration.GetSection(AquaAuthenticationOptions.SectionName)).ValidateOnStart();
builder.Services.AddOptions<ProductionCycleSimulationOptions>()
    .Bind(builder.Configuration.GetSection(ProductionCycleSimulationOptions.SectionName)).ValidateOnStart();
builder.Services.AddOptions<DeviceControlOptions>()
    .Bind(builder.Configuration.GetSection(DeviceControlOptions.SectionName)).ValidateOnStart();

builder.Services.AddSingleton<IValidateOptions<MongoDbOptions>, MongoDbOptionsValidator>();
builder.Services.AddSingleton<IValidateOptions<SensorThresholdCatalog>, SensorThresholdCatalogValidator>();
builder.Services.AddSingleton<IValidateOptions<SimulationProfileOptions>, SimulationProfileValidator>();
builder.Services.AddSingleton<IValidateOptions<SensorConnectionOptions>, SensorConnectionOptionsValidator>();
builder.Services.AddSingleton<IValidateOptions<SystemBehaviorOptions>, SystemBehaviorOptionsValidator>();
builder.Services.AddSingleton<IValidateOptions<DashboardSimulationOptions>, DashboardSimulationOptionsValidator>();
builder.Services.AddSingleton<IValidateOptions<AquaAuthenticationOptions>, AuthenticationOptionsValidator>();
builder.Services.AddSingleton<IValidateOptions<ProductionCycleSimulationOptions>, ProductionCycleSimulationOptionsValidator>();
builder.Services.AddSingleton<IValidateOptions<DeviceControlOptions>, DeviceControlOptionsValidator>();

builder.Services.AddSingleton<MongoContext>();
builder.Services.AddSingleton<FarmRepository>();
builder.Services.AddSingleton<MongoDatabaseInitializer>();
builder.Services.AddSingleton<FarmRuntimeState>();
builder.Services.AddSingleton<SensorMessageBus>();
builder.Services.AddSingleton<DataSourceHealthService>();
builder.Services.AddSingleton<AlertEngineService>();
builder.Services.AddSingleton<AutomationEngineService>();
builder.Services.AddScoped<DashboardAnalyticsService>();
builder.Services.AddSingleton<PasswordHasher>();
builder.Services.AddSingleton<UserAccountService>();
builder.Services.AddSingleton<ProductionCycleService>();
builder.Services.AddSingleton<FarmAccessService>();
builder.Services.AddSingleton<IDeviceCommandTransport, SimulatedDeviceCommandTransport>();
builder.Services.AddSingleton<DeviceCommandService>();

// Persist cookie encryption keys outside the container's writable layer so deployments and
// container recreation do not invalidate every active session. Production overrides this path.
var dataProtectionPath = builder.Configuration["DataProtection:KeysPath"]
    ?? Path.Combine(builder.Environment.ContentRootPath, ".data-protection-keys");
Directory.CreateDirectory(dataProtectionPath);
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(dataProtectionPath))
    .SetApplicationName("AquaOps");

var authOptions = builder.Configuration.GetSection(AquaAuthenticationOptions.SectionName).Get<AquaAuthenticationOptions>() ?? new();
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/login";
        options.AccessDeniedPath = "/access-denied";
        options.Cookie.Name = "AquaOps.Auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Strict;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        options.ExpireTimeSpan = TimeSpan.FromHours(Math.Max(1, authOptions.CookieHours));
        options.SlidingExpiration = true;
    });
builder.Services.AddAuthorization(options =>
{
    // Do not use a global fallback policy here. A fallback policy also applies to
    // static assets and the anonymous Blazor bootstrap endpoints, which can block
    // app.css/app.js on the public home and login pages. Protected pages declare
    // [Authorize] explicitly and therefore use this default policy.
    options.DefaultPolicy = new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
    options.AddPolicy("ManageUsers", p => p.RequireRole(SystemRoles.Administrator));
    options.AddPolicy("ManageFarm", p => p.RequireRole(SystemRoles.Administrator, SystemRoles.FarmManager));
    options.AddPolicy("OperatePonds", p => p.RequireRole(SystemRoles.Administrator, SystemRoles.FarmManager, SystemRoles.Technician));
});
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("login", http => RateLimitPartition.GetFixedWindowLimiter(
        http.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 10,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
            AutoReplenishment = true
        }));
});

builder.Services.AddSingleton<MongoWriteQueue>();
builder.Services.AddSingleton<IMongoWriteQueue>(sp => sp.GetRequiredService<MongoWriteQueue>());

builder.Services.AddSingleton<ISensorDataSource, SimulatedSensorSource>();
builder.Services.AddSingleton<ISensorDataSource, MqttSensorSource>();
builder.Services.AddSingleton<ISensorDataSource, ModbusTcpSensorSource>();
builder.Services.AddSingleton<ISensorDataSource, ModbusRtuSensorSource>();

builder.Services.AddScoped<FarmStateService>();
builder.Services.AddHealthChecks().AddCheck<MongoDbHealthCheck>("mongodb", tags: ["ready"]);

builder.Services.AddHostedService<MongoBootstrapHostedService>();
builder.Services.AddHostedService<MongoWriteQueue>(sp => sp.GetRequiredService<MongoWriteQueue>());
builder.Services.AddHostedService<SensorIngestionWorker>();
builder.Services.AddHostedService<SensorSourceCoordinator>();
builder.Services.AddHostedService<SensorOfflineMonitor>();
builder.Services.AddHostedService<AssetMaintenanceMonitor>();
builder.Services.AddHostedService<ProductionCycleSimulationHostedService>();

var app = builder.Build();

// Local recovery mode for an operator who has lost every administrator password. The secret is
// supplied through a short-lived environment variable and is never accepted as a command-line
// argument, where it could leak through shell history or the process list.
if (resetAdminPasswordMode)
{
    var username = Environment.GetEnvironmentVariable("AQUAOPS_RESET_USERNAME")?.Trim();
    var password = Environment.GetEnvironmentVariable("AQUAOPS_RESET_PASSWORD");
    if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
        throw new InvalidOperationException("AQUAOPS_RESET_USERNAME và AQUAOPS_RESET_PASSWORD là bắt buộc.");

    await app.Services.GetRequiredService<UserAccountService>()
        .ResetAdministratorPasswordAsync(username, password, CancellationToken.None);
    Environment.SetEnvironmentVariable("AQUAOPS_RESET_PASSWORD", null);
    app.Logger.LogWarning("Đã recovery mật khẩu và mở khóa tài khoản Administrator {Username}.", username);
    return;
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
}

// The deployment is HTTP-only on a trusted LAN and has no reverse proxy. Serve UI assets before
// authorization so the landing and login pages remain styled without an authentication cookie.
app.UseStaticFiles();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();
app.MapStaticAssets().AllowAnonymous();
// Liveness proves that the process can serve HTTP; readiness additionally checks MongoDB.
app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false }).AllowAnonymous();
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = check => check.Tags.Contains("ready") }).AllowAnonymous();
app.MapHealthChecks("/health").AllowAnonymous();
app.MapPost("/auth/login", async (HttpContext http, IAntiforgery antiforgery, UserAccountService accounts, CancellationToken ct) =>
{
    await antiforgery.ValidateRequestAsync(http);
    var form = await http.Request.ReadFormAsync(ct);
    var username = form["username"].ToString(); var password = form["password"].ToString(); var returnUrl = form["returnUrl"].ToString();
    var result = await accounts.ValidateCredentialsAsync(username, password, ct);
    if (!result.Success || result.User is null)
    {
        var error = result.Error.Contains("tạm khóa", StringComparison.OrdinalIgnoreCase) ? "locked" : "invalid";
        return Results.Redirect($"/login?error={error}&returnUrl={Uri.EscapeDataString(string.IsNullOrWhiteSpace(returnUrl)?"/dashboard":returnUrl)}");
    }
    await http.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, accounts.CreatePrincipal(result.User));
    var safeReturnUrl = !string.IsNullOrWhiteSpace(returnUrl) &&
                        returnUrl.StartsWith('/') &&
                        !returnUrl.StartsWith("//", StringComparison.Ordinal) &&
                        !returnUrl.StartsWith("/\\", StringComparison.Ordinal)
        ? returnUrl
        : "/dashboard";
    return Results.Redirect(safeReturnUrl);
}).AllowAnonymous().RequireRateLimiting("login");
app.MapPost("/auth/logout", async (HttpContext http, IAntiforgery antiforgery) =>
{
    await antiforgery.ValidateRequestAsync(http);
    await http.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    return Results.Redirect("/login");
}).RequireAuthorization();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
