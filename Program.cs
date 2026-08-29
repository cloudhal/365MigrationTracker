using _365MigrationTracker.Components;
using _365MigrationTracker.Configuration;
using _365MigrationTracker.Data;
using _365MigrationTracker.Services;
using MudBlazor.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Configure SQLite database
var dataDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "365MigrationTracker");
Directory.CreateDirectory(dataDir);
var dbPath = Path.Combine(dataDir, "tracker.db");
var connectionString = $"Data Source={dbPath}";

builder.Services.AddDbContext<DashboardDbContext>(options =>
    options.UseSqlite(connectionString));

// Configure collection options
builder.Services.Configure<CollectionOptions>(
    builder.Configuration.GetSection(CollectionOptions.SectionName));

// Register services
builder.Services.AddScoped<IMetricSnapshotStore, SqliteMetricSnapshotStore>();
builder.Services.AddScoped<IMigrationMetricsSource, SimulatedMetricsSource>();
builder.Services.AddScoped<MetricsCollectionService>();
builder.Services.AddScoped<ProgressCalculationService>();
builder.Services.AddHostedService<MetricsCollectionBackgroundService>();

builder.Services.AddMudServices();

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

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();

