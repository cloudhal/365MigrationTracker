using _365MigrationTracker.Components;
using _365MigrationTracker.Configuration;
using _365MigrationTracker.Data;
using _365MigrationTracker.Services;
using MudBlazor.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Blazored.LocalStorage;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Identity.Web;
using Microsoft.Identity.Web.UI;

var builder = WebApplication.CreateBuilder(args);

// ---------------------------------------------------------------------------
// Authentication - multi-tenant Microsoft Entra sign-in
//
// TenantId is "organizations" rather than a specific tenant, so any work or
// school account can sign in once their admin has consented and the app appears
// as an Enterprise Application in their tenant.
//
// WHO may sign in is deliberately not decided here. It is controlled per customer
// on the Enterprise Application: set "Assignment required?" to Yes, then manage
// the Users and groups page. Left at No, anyone in that tenant can sign in.
//
// Graph is called with DELEGATED permissions as the signed-in user, so the data
// shown is always that user's own tenant - there is no tenant id in configuration.
// ---------------------------------------------------------------------------
var graphScopes = builder.Configuration
    .GetSection($"{GraphOptions.SectionName}:Scopes")
    .Get<string[]>() ?? Array.Empty<string>();

builder.Services
    .AddAuthentication(OpenIdConnectDefaults.AuthenticationScheme)
    .AddMicrosoftIdentityWebApp(builder.Configuration.GetSection("AzureAd"))
    .EnableTokenAcquisitionToCallDownstreamApi(graphScopes)
    .AddInMemoryTokenCaches();

// ---------------------------------------------------------------------------
// Turn a failed sign-in into a page that explains itself.
//
// Without this, an admin who clicks Cancel on the consent screen gets an unhandled
// AuthenticationFailureException - a raw error page in development, a blank 500 in
// production - with nothing telling them what happened or how to retry. Declining
// consent is a normal thing for a cautious admin to do while evaluating the app.
//
// Configure runs after AddMicrosoftIdentityWebApp has set its own handlers, so the
// existing delegate is captured and invoked first rather than replaced.
// ---------------------------------------------------------------------------
builder.Services.Configure<OpenIdConnectOptions>(
    OpenIdConnectDefaults.AuthenticationScheme,
    options =>
    {
        var existingHandler = options.Events.OnRemoteFailure;

        options.Events.OnRemoteFailure = async context =>
        {
            if (existingHandler is not null)
            {
                await existingHandler(context);

                // Something upstream already produced a response; leave it alone.
                if (context.Result is not null) return;
            }

            var message = context.Failure?.Message ?? string.Empty;

            // AADSTS65004 is specifically "user declined to consent".
            var reason = message.Contains("AADSTS65004", StringComparison.OrdinalIgnoreCase)
                         || message.Contains("access_denied", StringComparison.OrdinalIgnoreCase)
                ? "consent_declined"
                : "failed";

            context.HandleResponse();
            context.Response.Redirect($"/signin-problem?reason={reason}");
        };
    });

builder.Services.AddAuthorization(options =>
{
    // Everything requires a signed-in user unless explicitly marked otherwise.
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});

builder.Services.AddCascadingAuthenticationState();

// Lets Blazor components surface incremental-consent and conditional-access
// challenges as a redirect instead of an unhandled exception.
builder.Services.AddMicrosoftIdentityConsentHandler();

// Provides the /MicrosoftIdentity/Account/SignIn and SignOut endpoints.
builder.Services.AddControllersWithViews()
    .AddMicrosoftIdentityUI();

// ---------------------------------------------------------------------------
// Database
//
// The connection string comes from configuration when present, so a deployment can
// point at persistent storage without a code change. On App Service only /home is
// persisted and shared between instances, e.g.
//     ConnectionStrings__Tracker = Data Source=/home/data/tracker.db
// Anywhere else on the filesystem is wiped on restart or redeploy.
//
// SQLite over the Azure Files share behind /home is acceptable for a single instance
// but does not tolerate scale-out - its file locking is unreliable over SMB. Move to
// Azure SQL or Postgres before scaling past one instance.
// ---------------------------------------------------------------------------
var connectionString = builder.Configuration.GetConnectionString("Tracker");

if (string.IsNullOrWhiteSpace(connectionString))
{
    // Local development default
    var dataDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "365MigrationTracker");
    Directory.CreateDirectory(dataDir);
    connectionString = $"Data Source={Path.Combine(dataDir, "tracker.db")}";
}

builder.Services.AddDbContext<DashboardDbContext>(options =>
    options.UseSqlite(connectionString));

// ---------------------------------------------------------------------------
// Data protection keys
//
// Blazor Server signs auth cookies and antiforgery tokens with these keys. Held in
// memory by default, which means every App Service restart or swap silently signs
// everyone out, and a scaled-out app rejects tokens issued by a sibling instance.
// Point DataProtection:KeyRingPath at persistent storage in a deployment, e.g.
//     DataProtection__KeyRingPath = /home/dataprotection-keys
// ---------------------------------------------------------------------------
var keyRingPath = builder.Configuration["DataProtection:KeyRingPath"];

if (!string.IsNullOrWhiteSpace(keyRingPath))
{
    Directory.CreateDirectory(keyRingPath);
    builder.Services
        .AddDataProtection()
        .PersistKeysToFileSystem(new DirectoryInfo(keyRingPath))
        .SetApplicationName("365MigrationTracker");
}

// Configure collection options
builder.Services.Configure<CollectionOptions>(
    builder.Configuration.GetSection(CollectionOptions.SectionName));

// Configure Graph API options
builder.Services.Configure<GraphOptions>(
    builder.Configuration.GetSection(GraphOptions.SectionName));

// Register services
builder.Services.AddScoped<IMetricSnapshotStore, SqliteMetricSnapshotStore>();

// Register metrics source based on configuration
builder.Services.AddScoped<IMigrationMetricsSource>(provider =>
{
    var collectionOptions = provider.GetRequiredService<IOptions<CollectionOptions>>();
    var logger = provider.GetRequiredService<ILoggerFactory>().CreateLogger<IMigrationMetricsSource>();

    if (collectionOptions.Value.Source.Equals("Graph", StringComparison.OrdinalIgnoreCase))
    {
        logger.LogInformation("Using Graph API metrics source");
        return provider.GetRequiredService<GraphMigrationMetricsSource>();
    }

    logger.LogInformation("Using Simulated metrics source");
    return provider.GetRequiredService<SimulatedMetricsSource>();
});

// Register both implementations for factory pattern
builder.Services.AddScoped<SimulatedMetricsSource>();
builder.Services.AddScoped<GraphMigrationMetricsSource>();

// Supplies the individual directory objects behind the drill-down grids
builder.Services.AddScoped<IDirectorySource, GraphDirectorySource>();

// Delegated Graph access token for the signed-in user, plus their tenant id
builder.Services.AddScoped<IGraphAccessProvider, DelegatedGraphAccessProvider>();

builder.Services.AddScoped<MetricsCollectionService>();
builder.Services.AddScoped<ProgressCalculationService>();

// No background collection service.
//
// Graph is now called with delegated permissions as the signed-in user, so there is
// no credential to collect with when nobody is signed in. Snapshots are instead
// written when someone opens the dashboard - see SnapshotOnVisitService.
builder.Services.AddScoped<SnapshotOnVisitService>();

builder.Services.AddMudServices();

// Browser localStorage, used to persist UI preferences such as dark mode
builder.Services.AddBlazoredLocalStorage();

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
    
var app = builder.Build();

// Apply EF Core migrations automatically
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<DashboardDbContext>();
    await db.Database.MigrateAsync();
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.UseAntiforgery();

app.MapStaticAssets();

// /MicrosoftIdentity/Account/SignIn and SignOut
app.MapControllers();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();

