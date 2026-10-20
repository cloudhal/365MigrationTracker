# 365 Migration Tracker

A maintainable internal dashboard for tracking migration progress from on-premises Active Directory to Microsoft Entra ID. Built with .NET 10, ASP.NET Core Blazor, and MudBlazor.

## Overview

This application monitors key migration metrics over time:

- **Users still synced from on-premises AD**, and **users migrated** (synced at some point, now cloud-only)
- **Groups still synced from on-premises AD**, and **groups migrated**
- **Microsoft Entra hybrid-joined devices**
- **Pending hybrid device registrations**
- **Microsoft Entra-joined devices** (for comparison)

The app runs in two modes, selected by `MetricsCollection:Source`:
- **Simulated**: generated data, for demos and testing without a tenant.
- **Graph**: live counts from Microsoft Graph, using the signed-in user's delegated permissions. Used in production.

Snapshots are stored in SQLite. The app signs users in with Microsoft Entra ID (multi-tenant, "organizations").

## Quick Start

### Prerequisites

- .NET 10 SDK or later
- Visual Studio Code (recommended) or Visual Studio 2022+
- SQLite (bundled with EF Core)

### Setup & Run

1. **Restore dependencies:**
   ```bash
   dotnet restore
   ```

2. **Apply database migrations:**
   ```bash
   dotnet ef database update
   ```
   Or let the app auto-migrate on first run (configured in `Program.cs`).

3. **Run the application:**
   ```bash
   dotnet run
   ```
   The app launches at `https://localhost:5001`

4. **Manual collection:**
   - Navigate to the Dashboard (`/` or `/dashboard`)
   - Click "Collect Now" to trigger a metrics collection cycle
   - In development, the background service also collects every 5 minutes

### Build for Release

```bash
dotnet build -c Release
```

### Run Tests

```bash
dotnet test
```

Note: Tests are written (in `Tests/`) and compile successfully. Full test runner integration may require a dedicated test project.

## Project Structure

```
365MigrationTracker/
├── Components/
│   ├── Pages/
│   │   ├── Dashboard.razor          # Main metrics dashboard
│   │   ├── Counter.razor            # (Template - can be removed)
│   │   ├── Weather.razor            # (Template - can be removed)
│   │   └── Error.razor              # Error page
│   ├── Layout/
│   │   ├── MainLayout.razor         # MudBlazor main layout with dark mode
│   │   └── NavMenu.razor            # Navigation menu
│   ├── App.razor                    # Root component
│   └── _Imports.razor               # Global usings
├── Configuration/
│   └── CollectionOptions.cs         # Metrics collection settings
├── Data/
│   ├── DashboardDbContext.cs        # EF Core DbContext
│   ├── MetricSnapshot.cs            # Snapshot entity model
│   └── Migrations/
│       └── 20260828173514_InitialCreate.cs
├── Models/
│   ├── MigrationMetrics.cs          # Current metrics POCO
│   ├── CollectionResult.cs          # Collection attempt result
│   └── ProgressMetrics.cs           # Calculated progress data
├── Services/
│   ├── IMigrationMetricsSource.cs   # Metrics retrieval abstraction
│   ├── SimulatedMetricsSource.cs    # Simulated data generator
│   ├── IMetricSnapshotStore.cs      # Storage abstraction
│   ├── SqliteMetricSnapshotStore.cs # SQLite implementation
│   ├── MetricsCollectionService.cs  # Collection orchestrator
│   ├── MetricsCollectionBackgroundService.cs  # Scheduled task
│   └── ProgressCalculationService.cs # Progress calculation helper
├── Tests/
│   ├── SimulatedMetricsSourceTests.cs
│   ├── MetricsCollectionServiceTests.cs
│   └── Usings.cs
├── appsettings.json                 # Production config
├── appsettings.Development.json     # Development config (5-min intervals)
├── Program.cs                       # DI & middleware setup
└── README.md                        # This file
```

## Configuration

### Collection Settings

Edit `appsettings.json` to configure metrics collection:

```json
{
  "MetricsCollection": {
    "Enabled": true,
    "IntervalHours": 6,
    "Source": "Simulated",
    "StaleDataThresholdHours": 12
  }
}
```

- **Enabled:** Turn collection on/off
- **IntervalHours:** Frequency of automated collection (decimals supported: 0.083 ≈ 5 minutes)
- **Source:** `"Simulated"` or `"Graph"`
- **StaleDataThresholdHours:** Dashboard warning threshold

### Graph Settings

```json
{
  "GraphApi": {
    "Scopes": [ "User.Read.All", "Group.Read.All", "Device.Read.All" ],
    "PageSize": 999,
    "MaxRetries": 3,
    "RetryDelayMilliseconds": 1000,
    "TimeoutSeconds": 30
  }
}
```

Scopes are delegated: the effective access is these scopes intersected with the signed-in user's own directory roles. The app is multi-tenant (`TenantId` is `organizations`), so the tenant is whichever one the signed-in user belongs to. No tenant ID is configured.

### Sign-in Credentials

- **Local development:** `AzureAd:ClientId` and `AzureAd:ClientSecret` go in User Secrets (`dotnet user-secrets set`). Never in `appsettings.json`.
- **Production (`appsettings.Production.json`):** no secret. The App Service uses its system-assigned managed identity with workload identity federation (`SignedAssertionFromManagedIdentity`). `AzureAd:ClientId` is still required and is set as an App Setting.

### Development vs Production

**Development** (`appsettings.Development.json`):
- IntervalHours: 0.083 (≈ 5 minutes) for rapid testing

**Production** (`appsettings.json`):
- IntervalHours: 6 for standard monitoring
- Source: `"Graph"`

### Environment Variables (App Service)

| Setting | Value |
|---|---|
| `AzureAd__ClientId` | Application (client) ID of the app registration |
| `ConnectionStrings__Tracker` | `Data Source=/home/data/tracker.db` |
| `DataProtection__KeyRingPath` | `/home/dataprotection-keys` |

Settings with `__` map to the nested config keys. `/home` is the only persistent storage on App Service Linux.

## Database

### Initialization

EF Core migrations are applied automatically on app startup. The database is created in:

```
%APPDATA%\365MigrationTracker\tracker.db
```

(Excluded from source control via `.gitignore`)

### Schema

**MetricSnapshot** table with indexed columns:
- `Id` (PK)
- `CapturedAtUtc` (indexed) — UTC timestamp
- `TenantId` — Entra tenant of the signed-in user (indexed with `CapturedAtUtc`). Every snapshot query filters on it.
- `SyncedUsers`, `MigratedUsers`, `SyncedGroups`, `MigratedGroups`, `CloudOnlyGroups`, `HybridDevices`, `PendingHybridDevices`, `EntraJoinedDevices`, `TotalUsers`, `TotalGroups`, `TotalDevices` — All non-negative integers
- `Source` — `"Graph"` or `"Simulated"`. Rows from the two sources are not comparable, so trends should not mix them.
- `CollectionSucceeded` (indexed) — Boolean
- `CollectionDurationMilliseconds` — Long
- `ErrorMessage` — Nullable string (max 500 chars, no PII)

Migrations added for these columns: `AddTenantIdToMetricSnapshot`, `AddSourceToMetricSnapshot`, `AddMigratedUsersToMetricSnapshot`, `AddMigratedAndCloudOnlyGroupsToMetricSnapshot`. `Program.cs` applies pending migrations on startup.

### Running Migrations

Create a new migration:
```bash
dotnet ef migrations add MigrationName --output-dir Data/Migrations
```

Apply migrations:
```bash
dotnet ef database update
```

Remove last migration (undo):
```bash
dotnet ef migrations remove
```

## Features

### Dashboard

- **Donut charts:** Users (overall: cloud-only vs on-prem origin), Users (on-prem origin: still synced vs migrated), Groups (overall), Groups (on-prem origin: still synced vs migrated), Windows devices (hybrid / Entra / other)
- **Metric Cards:** Migrated % for users and groups (with "X of Y"), and hybrid-joined devices with change indicators (↓ = progress, ↑ = concern)
- **Drill-down pages** (`/details/users`, `/details/groups`, `/details/devices`): live Graph lists. The devices grid shows DeviceId.
- **Progress Bars:** % migrated since baseline (capped 0-100%)
- **Collection Status:** Last collection time, duration, success/failure
- **Manual Collection:** "Collect Now" button with loading state
- **Trend View:** Placeholder for charts; date range selector (7/30/90 days, all data)
- **Recent Failures:** Last 5 failed collection attempts
- **Stale Data Warning:** Alert if data is older than configured threshold
- **Responsive Layout:** MudBlazor components; dark mode support

### Collection Service

- **Manual Trigger:** Click "Collect Now" on dashboard
- **Scheduled Execution:** Background service runs on configurable interval
- **Concurrency Protection:** Prevents overlapping collection runs via semaphore
- **Error Handling:** Failed collections are logged and recorded (not shown as valid data)
- **Logging:** Detailed collection outcomes via ILogger

### Simulated Metrics

Starting baseline:
- 5,000 synced users → 0 (target)
- 250 synced groups → 0 (target)
- 3,500 hybrid devices → 500 (target)
- 150 pending hybrid → 0 (target)
- 1,500 Entra-joined → 4,000 (target)

Progression:
- Changes ~0.5% per hour with ±2% random variance
- Never produces negative counts
- Deterministic enough for testing

### Metric Definitions (Graph)

Each count is a `$count=true` request in `Services/GraphMigrationMetricsSource.cs`. Device counts are restricted to `operatingSystem eq 'Windows'`.

| Metric | Graph query filter | Notes |
|---|---|---|
| Users synced from AD | `users?$filter=onPremisesSyncEnabled eq true` | |
| Users migrated | `users?$filter=onPremisesSyncEnabled eq false` | Cloud-only accounts show `null` here, not `false`, so they are not counted |
| Groups synced from AD | `groups?$filter=onPremisesSyncEnabled eq true` | |
| Groups never on-prem | `groups?$filter=onPremisesSecurityIdentifier eq null` | Used with the total to find groups of on-prem origin |
| Groups migrated | (total groups − cloud-only groups) − synced groups | Calculated, not queried |
| Hybrid devices | `devices?$filter=trustType eq 'ServerAd' and operatingSystem eq 'Windows'` | |
| Entra-joined devices | `devices?$filter=trustType eq 'AzureAd' and operatingSystem eq 'Windows'` | |
| Total users / groups / devices | no filter / no filter / Windows only | |

Percentages on the dashboard:
- Users migrated % = migrated users ÷ (synced + migrated)
- Groups migrated % = migrated groups ÷ (synced + migrated)

The `Math.Max(0, …)` guards keep a stale or inconsistent total from producing a negative count.

Filters that were tried and do not work:
- `onPremisesUserPrincipalName ne null`: Graph returns `400 Request_UnsupportedQuery` ("An unsupported property was specified"). That property is not filterable.

## Architecture & Design

### Service Abstractions

**IMigrationMetricsSource** — Metrics retrieval interface
- Current: `SimulatedMetricsSource` and `GraphMigrationMetricsSource` (selected by `MetricsCollection:Source`)
- Future: none planned for the metrics source

**IMetricSnapshotStore** — Snapshot persistence interface
- Current: `SqliteMetricSnapshotStore`
- Future: `AzureTableMetricSnapshotStore`

This separation allows swapping implementations without changing business logic.

### Progress Calculations

`ProgressCalculationService` computes:
- Absolute change (current - previous)
- Percentage change ((change / previous) × 100)
- Percentage migrated ((baseline - current) / baseline) × 100

Safe handling of:
- No snapshots yet
- Only one snapshot (no change calculation)
- Zero baselines (no % migrated)
- Temporary increases (still displayed)

### No PII Storage

The application does not store:
- User names or UPNs
- Group names or object IDs
- Device names or identifiers
- Access tokens

Only counts and timestamps are persisted.

## Future Work

### Phase 2: Microsoft Graph Integration — done

- `GraphMigrationMetricsSource` implements the Graph queries in *Metric Definitions*.
- Delegated read-only scopes: `User.Read.All`, `Group.Read.All`, `Device.Read.All`.
- Retries with backoff on transient errors. Failed queries are recorded as failed snapshots, never as zeros.
- Collection happens on the signed-in user's visit and on "Collect Now" (`SnapshotOnVisitService`). There is no background collector, because Graph is called with the user's own token.

### Phase 3: Azure Deployment — in progress

- Deployed to Azure App Service (Linux B1, .NET 10) with a system-assigned managed identity and workload identity federation. There is no client secret in production.
- Remaining: move the repo to Azure DevOps and enable `azure-pipelines.yml`.
- Still on SQLite. Next: Azure SQL (replace `SqliteMetricSnapshotStore`), then scale-out.

### Phase 4: Authentication & Authorization — partly done

- Done: Microsoft Entra sign-in via `Microsoft.Identity.Web` (multi-tenant, "organizations"). Sign-in is controlled per customer by "Assignment required" on the Enterprise Application.
- To do: restrict to an application role or security group in the app, not only on the Enterprise Application.

## Testing

### Unit Tests (in `Tests/`)

**SimulatedMetricsSourceTests:**
- Non-negative count validation
- Duration measurement
- Cancellation handling
- Realistic progression

**MetricsCollectionServiceTests:**
- Successful snapshot saving
- Failed collection recording
- Concurrency protection
- Collection state flags

Run tests:
```bash
dotnet test
```

### Integration Testing

For SQL integration tests:
```csharp
var connection = new SqliteConnection("Data Source=:memory:");
connection.Open();
var options = new DbContextOptionsBuilder<DashboardDbContext>()
    .UseSqlite(connection)
    .Options;
using var db = new DashboardDbContext(options);
db.Database.EnsureCreated();
// Test snapshot operations
```

## Deployment

### Local Development

```bash
dotnet run --configuration Development
```

App runs at `https://localhost:5001`

### Release Build

```bash
dotnet build -c Release
dotnet publish -c Release -o ./publish
```

### Azure App Service (current production)

| Item | Value |
|---|---|
| Resource group | `<resource-group>` |
| App Service plan | `appserviceplan-prod-01` (Linux, B1) |
| Web app | `365migrationdashboard-prod-01` |
| Runtime stack | `DOTNETCORE|10.0` |
| Database | SQLite at `/home/data/tracker.db` (single instance only) |

**One-time setup**

1. **Managed identity:** Web app > Settings > Identity > System assigned > On.
2. **Federated credential:** App registration > Certificates & secrets > Federated credentials > Add credential > scenario "Managed identity", pointing at the web app's identity.
3. **Redirect URIs:** App registration > Authentication, add `https://365migrationdashboard-prod-01.azurewebsites.net/signin-oidc` and `https://365migrationdashboard-prod-01.azurewebsites.net/signout-callback-oidc`.
4. **App Settings:** Web app > Settings > Environment variables > App settings. Add the three values in *Environment Variables (App Service)* above. Confirm `ASPNETCORE_ENVIRONMENT` is `Production`.
5. **Admin consent:** Grant tenant admin consent for `User.Read.All`, `Group.Read.All` and `Device.Read.All` on the app registration, then assign users who may sign in.

**Manual deploy (PowerShell)**

```powershell
$resourceGroupName = "<resource-group>"
$webAppName        = "365migrationdashboard-prod-01"

dotnet publish 365MigrationTracker.csproj -c Release -o "$env:TEMP\publish"
Compress-Archive -Path "$env:TEMP\publish\*" -DestinationPath "$env:TEMP\tracker.zip" -Force
Publish-AzWebApp -ResourceGroupName $resourceGroupName -Name $webAppName -ArchivePath "$env:TEMP\tracker.zip" -Force
```

**Planned: Azure DevOps pipeline**

`azure-pipelines.yml` builds on Ubuntu with .NET 10 and deploys the app with `AzureWebApp@1` (`appType: webAppLinux`). Before using it:
- Move the repo from GitHub to Azure DevOps (or connect the pipeline to GitHub).
- Create an Azure Resource Manager service connection and put its name in `azureSubscription`.
- Replace `<azure-resource-manager-service-connection-name>` in the file.

### Running the Published Build Locally

```bash
dotnet 365MigrationTracker.dll
```

## Troubleshooting

### Database issues

**"The database is locked"**
- Close any other instances accessing the database
- Delete `%APPDATA%/365MigrationTracker/tracker.db` and restart

**"Migration not applied"**
- Run `dotnet ef database update` manually
- Check `%APPDATA%/365MigrationTracker/` exists and is writable

### Collection not running

**Check logs** in the application console output

**Verify settings** in `appsettings.json`:
- `Enabled: true`
- `IntervalHours` is reasonable (not zero)
- `Source` is "Simulated"

### Dashboard not loading data

- Click "Collect Now" manually to generate initial data
- Wait for background service (5 min dev, 6 hours prod)
- Check for errors in browser console (F12 → Console tab)

### Graph collection fails

**"Collection failed: Graph API collection failed: Response status code does not indicate success: 400 (Bad Request)"**
- The console log shows the URL and Graph's response body. Look for `Request_UnsupportedQuery`, which means a property is not filterable (see Metric Definitions).

**"Pending model changes" on startup (`dotnet watch`)**
- The EF model has a property that no migration covers. Add a migration (`dotnet ef migrations add ...`) and update `DashboardDbContextModelSnapshot.cs`.

**"Unable to open database file" on App Service**
- Check `ConnectionStrings__Tracker` points at `/home/data/tracker.db`. `Program.cs` creates the directory on startup.
- `dotnet watch` or a running local app locks the `apphost.exe` during build. Stop it before rebuilding.

### App Service

- After changing App Settings, the app restarts. Check Log stream for startup errors.
- A sign-in loop or `AADSTS` error usually means the redirect URIs or the federated credential do not match the web app's managed identity.
- Only one instance is supported while the database is SQLite. Scaling out needs Azure SQL first.

## Support & Contributing

This is an internal tool. For issues or enhancements:
1. Check the runbook for design rationale
2. Review `Program.cs` and service abstractions
3. Add tests before implementing features
4. Do not commit secrets or PII

## License

Internal use only. Not for external distribution.

---

## Checklist: MVP Definition of Done

- ✅ Application starts successfully
- ✅ SQLite initialized via EF Core migrations
- ✅ Simulated snapshots collected manually ("Collect Now")
- ✅ Scheduled collection runs (configurable interval)
- ✅ Overlapping collections prevented
- ✅ Current metrics displayed on dashboard
- ✅ Failed collections handled visibly and safely
- ✅ No secrets or personal directory data stored
- ✅ Automated tests written (unit + mock tests)
- ✅ Release build succeeds
- ✅ README explains setup for another developer
- ✅ Dashboard responsive and accessible

## Summary

The 365 Migration Tracker MVP provides a solid foundation for monitoring Entra migration progress. It:
- Collects metrics reliably with concurrency protection
- Displays progress intuitively with calculations
- Stores only necessary data (no PII)
- Separates concerns via abstractions for future integration
- Includes comprehensive logging and error handling
- Uses responsive, accessible MudBlazor UI

Next steps: Graph integration, Azure deployment, and Entra authentication.
