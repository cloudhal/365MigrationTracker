# 365 Migration Tracker

A maintainable internal dashboard for tracking migration progress from on-premises Active Directory to Microsoft Entra ID. Built with .NET 10, ASP.NET Core Blazor, and MudBlazor.

## Overview

This application monitors key migration metrics over time:

- **Users still synced from on-premises AD**
- **Groups still synced from on-premises AD**
- **Microsoft Entra hybrid-joined devices**
- **Pending hybrid device registrations**
- **Microsoft Entra-joined devices** (for comparison)

The MVP runs locally with simulated data and SQLite. The architecture is designed to support future integration with Microsoft Graph and Azure Table Storage without major refactoring.

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
- **Source:** "Simulated" (future: "Graph")
- **StaleDataThresholdHours:** Dashboard warning threshold

### Development vs Production

**Development** (`appsettings.Development.json`):
- IntervalHours: 0.083 (≈ 5 minutes) for rapid testing

**Production** (`appsettings.json`):
- IntervalHours: 6 for standard monitoring

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
- `SyncedUsers`, `SyncedGroups`, `HybridDevices`, `PendingHybridDevices`, `EntraJoinedDevices` — All non-negative integers
- `CollectionSucceeded` (indexed) — Boolean
- `CollectionDurationMilliseconds` — Long
- `ErrorMessage` — Nullable string (max 500 chars, no PII)

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

- **Metric Cards:** Current values with change indicators (↓ = progress, ↑ = concern)
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

## Architecture & Design

### Service Abstractions

**IMigrationMetricsSource** — Metrics retrieval interface
- Current: `SimulatedMetricsSource`
- Future: `GraphMigrationMetricsSource`

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

### Phase 2: Microsoft Graph Integration

1. Create `GraphMigrationMetricsSource` implementing `IMigrationMetricsSource`
2. Query Microsoft Graph v1.0:
   - Users: `onPremisesSyncEnabled == true`
   - Groups: `onPremisesSyncEnabled == true`
   - Devices: Filter by `trustType` and `profileType`
3. Use read-only permissions: `User.Read.All`, `Group.Read.All`, `Device.Read.All`
4. Handle pagination and throttling
5. Use managed identity (Azure) or user secrets (local dev)

### Phase 3: Azure Deployment

1. Create `AzureTableMetricSnapshotStore` implementing `IMetricSnapshotStore`
2. Deploy to Azure App Service with:
   - System-assigned managed identity
   - Always On enabled
   - HTTPS Only enabled
3. Replace SQLite with Azure Table Storage
4. Implement Microsoft Entra authentication via `Microsoft.Identity.Web`

### Phase 4: Authentication & Authorization

1. Add `Microsoft.Identity.Web` for Entra sign-in
2. Authorize access via:
   - Application role, or
   - Security group membership
3. Keep background collection (managed identity) separate from user authentication

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

Output in `./publish` folder. Copy to deployment target and run:
```bash
./365MigrationTracker.exe
```
(Or `dotnet 365MigrationTracker.dll` on non-Windows)

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
