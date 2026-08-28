


# Build an Entra Migration Progress Dashboard

Act as a senior .NET developer and build a small, maintainable internal dashboard that tracks progress in migrating identities and devices away from on-premises Active Directory.

## Objective

Create a Blazor application that records and displays these metrics over time:

1. Users still synchronized from on-premises AD.
2. Groups still synchronized from on-premises AD.
3. Computers that are Microsoft Entra hybrid joined.
4. Optionally, hybrid devices whose registration is still pending.
5. Microsoft Entra joined devices, for comparison.

The application must initially run locally using simulated data and SQLite. It should be structured so that simulated data can later be replaced by Microsoft Graph and SQLite can later be replaced by Azure Table Storage.

Do not provision Azure resources, create app registrations, modify a tenant, or request production credentials during the initial implementation.

## Technology choices

Use:

- .NET 10 LTS.
- ASP.NET Core Blazor Web App.
- Interactive Server render mode.
- Global interactivity.
- C# with nullable reference types enabled.
- Entity Framework Core with SQLite.
- Dependency injection throughout.
- Built-in logging and configuration.
- A clean, responsive interface using the standard Blazor styling or lightweight custom CSS.
- A simple accessible SVG chart if a charting library is not already present.

Do not create a separate WebAssembly project, web API, Aspire project, or JavaScript frontend.


### Creating the project in vs code:

dotnet new blazor `
  --name 365MigrationDashboard `
  --interactivity Server `
  --all-interactive

## Initial project structure

Aim for a structure similar to:

```text
EntraMigrationDashboard
├── Components
│   ├── Layout
│   ├── Pages
│   │   └── Dashboard.razor
│   └── Shared
├── Data
│   ├── DashboardDbContext.cs
│   ├── MetricSnapshot.cs
│   └── Migrations
├── Models
│   ├── MigrationMetrics.cs
│   └── CollectionResult.cs
├── Services
│   ├── IMigrationMetricsSource.cs
│   ├── SimulatedMetricsSource.cs
│   ├── IMetricSnapshotStore.cs
│   ├── SqliteMetricSnapshotStore.cs
│   ├── MetricsCollectionService.cs
│   └── MetricsCollectionBackgroundService.cs
├── Configuration
│   └── CollectionOptions.cs
├── Program.cs
├── appsettings.json
└── README.md
```

Adapt this structure if the existing repository already has clear conventions.

## Data model

Create a `MetricSnapshot` entity containing at least:

```text
Id
CapturedAtUtc
SyncedUsers
SyncedGroups
HybridDevices
PendingHybridDevices
EntraJoinedDevices
CollectionSucceeded
CollectionDurationMilliseconds
ErrorMessage
```

Requirements:

- Store timestamps in UTC.
- Add an index on `CapturedAtUtc`.
- Counts must be non-negative.
- `ErrorMessage` must be nullable.
- Failed collections may be recorded, but they must not be presented as successful metric data.
- Do not store user names, UPNs, group names, device names, object IDs, or other directory personal data.

## Service abstractions

Create an `IMigrationMetricsSource` interface that retrieves the current metrics.

Create a `SimulatedMetricsSource` implementation that:

- Produces realistic migration data.
- Starts with configurable baseline counts.
- Gradually decreases synced and hybrid counts.
- Gradually increases Entra-joined device counts.
- Never produces negative counts.
- Is deterministic enough to test.

Create an `IMetricSnapshotStore` abstraction that supports:

- Saving a snapshot.
- Retrieving the latest successful snapshot.
- Retrieving successful snapshots for a date range.
- Retrieving recent collection failures.

Use SQLite for its initial implementation.

This separation is important because later implementations will use:

- `GraphMigrationMetricsSource`
- `AzureTableMetricSnapshotStore`

Do not implement Azure Table Storage in the first phase.

## Collection behaviour

Create a collection coordinator that:

1. Requests metrics from the configured source.
2. Measures the collection duration.
3. Stores a successful snapshot.
4. Logs a useful summary.
5. Handles transient failures without crashing the application.
6. Records or logs failed collection attempts appropriately.
7. Prevents overlapping collection runs.

Support both:

- A configurable scheduled collection interval.
- A manual “Collect now” action on the dashboard.

Add configuration similar to:

```json
{
  "MetricsCollection": {
    "Enabled": true,
    "IntervalHours": 6,
    "Source": "Simulated"
  }
}
```

In development, make the interval easy to shorten for testing.

The background service must respect application cancellation and shut down cleanly.

## Dashboard requirements

Create a dashboard with:

- A prominent application title.
- A short explanation of what is being measured.
- One card for each current metric.
- The timestamp of the latest successful collection.
- The change in each metric since the previous snapshot.
- A progress indicator showing movement toward zero synced users, synced groups, and hybrid devices.
- A trend chart covering the most recent 30 days.
- A selectable range of 7 days, 30 days, 90 days, or all data.
- A “Collect now” button.
- A visible collecting state while collection is running.
- A clear success or failure message after manual collection.
- A warning if the latest data is older than the configured threshold.
- A small collection-health area showing recent failures.

Use wording such as:

- “Users still synced from on-premises AD”
- “Groups still synced from on-premises AD”
- “Microsoft Entra hybrid joined devices”
- “Microsoft Entra joined devices”

Do not label synced objects as “synced from Entra,” because the intended meaning is objects synchronized from on-premises AD into Entra ID.

Make the dashboard usable on a laptop and mobile screen. Use accessible colour contrast and do not communicate progress through colour alone.

## Progress calculations

For each declining migration metric, show:

- Current count.
- Previous count.
- Absolute change.
- Percentage change where mathematically valid.
- Baseline count.
- Percentage migrated since the baseline.

Handle these cases safely:

- No snapshots yet.
- Only one snapshot exists.
- Baseline is zero.
- A count temporarily increases.
- The latest collection failed.

Do not claim an estimated completion date in the first version. Migration progress is rarely linear, and a misleading forecast would be worse than omitting it.

## Database initialization

Use EF Core migrations.

Provide commands in the README for:

- Restoring dependencies.
- Creating or applying migrations.
- Running the application.
- Running tests.

Store the development SQLite file in a data directory that is excluded from source control.

Do not automatically delete, recreate, or overwrite an existing database.

## Testing

Add automated tests for at least:

- Progress calculations.
- Baseline-zero handling.
- Increasing counts.
- Simulated metrics never becoming negative.
- Saving and retrieving snapshots.
- Selecting only successful snapshots for charts.
- Preventing overlapping collection runs.
- Handling a source exception without crashing the application.

Prefer focused unit tests and a small SQLite integration test using an isolated temporary database.

## Configuration and secrets

Do not place credentials, tenant IDs, client secrets, connection strings containing secrets, or certificates in source-controlled configuration.

Add safe placeholders and document the intended use of:

- .NET user secrets for local development.
- App Service settings for deployment.
- Managed identity in Azure.

Ensure `.gitignore` excludes:

- Local SQLite database files.
- Development secrets.
- Build output.
- IDE-specific temporary files.

## Future Microsoft Graph integration

Do not connect to Graph until the simulated version is working and tested.

Prepare the design for a future `GraphMigrationMetricsSource` that will measure:

```text
Users:
onPremisesSyncEnabled == true

Groups:
onPremisesSyncEnabled == true

Hybrid devices:
trustType == "ServerAd"
and normally profileType == "RegisteredDevice"

Pending hybrid devices:
trustType == "ServerAd"
and profileType != "RegisteredDevice"

Entra joined devices:
trustType == "AzureAd"
```

The eventual Graph implementation should:

- Use Microsoft Graph v1.0.
- Use only read permissions.
- Use `User.Read.All`, `Group.Read.All`, and `Device.Read.All`.
- Handle pagination.
- Use advanced-query headers when required.
- Implement retry behaviour for throttling and transient failures.
- Respect `Retry-After`.
- Avoid downloading unnecessary properties.
- Never log access tokens or directory object details.
- Use a managed identity when hosted in Azure.
- Use a credential stored through .NET user secrets only if live Graph testing is required locally.

Treat the precise definition of “in-scope computer” as configuration. Later, we may decide to exclude disabled or stale device records.

## Future authentication

The dashboard will eventually use single-tenant Microsoft Entra authentication through `Microsoft.Identity.Web`.

Do not add local Individual Accounts or a separate username/password database.

Prepare for eventual authorization through either:

- An Entra application role, or
- Membership of an approved Entra security group.

Do not make the production dashboard anonymously accessible.

Authentication for people viewing the dashboard and the managed identity used for background Graph collection are separate security concerns. Keep them separate in the design.

## Future Azure deployment

The expected production architecture is:

```text
Browser
  → Microsoft Entra sign-in
  → Azure App Service
      → Managed identity
          → Microsoft Graph
          → Azure Table Storage
```

Expected deployment settings:

- One App Service instance initially.
- Basic tier or above.
- Always On enabled.
- HTTPS Only enabled.
- System-assigned managed identity.
- Azure Table Storage for snapshots.
- No stored Graph client secret.
- Appropriate logging without personal directory data.

Do not create these resources without explicit approval.

## Implementation sequence

Work in these checkpoints:

### Checkpoint 1: Inspect and scaffold

- Inspect the repository and any existing instructions.
- Confirm the installed .NET SDK.
- Scaffold the Blazor Web App if required.
- Run the untouched application.
- Report any environmental blockers.

### Checkpoint 2: Data layer

- Add the snapshot model.
- Add SQLite and EF Core.
- Add the initial migration.
- Add the snapshot-store abstraction and implementation.
- Add storage tests.

### Checkpoint 3: Simulated collection

- Add the metrics-source abstraction.
- Add the simulated source.
- Add scheduled and manual collection.
- Add concurrency protection.
- Add collection tests.

### Checkpoint 4: Dashboard

- Add metric cards.
- Add progress calculations.
- Add trends and date-range selection.
- Add collection status and stale-data warnings.
- Verify responsive and accessible presentation.

### Checkpoint 5: Documentation and verification

- Update the README.
- Run the complete test suite.
- Build in Release configuration.
- Review for secrets and personal data.
- Summarize what is complete and what remains.

Complete one checkpoint at a time. After each checkpoint:

1. Build the solution.
2. Run relevant tests.
3. Briefly report what changed.
4. Continue if there is no blocker.

## Definition of done for the local MVP

The local MVP is complete when:

- The application starts successfully.
- SQLite is initialized through EF Core migrations.
- Simulated snapshots can be collected manually.
- Scheduled collection works.
- Repeated clicks cannot start overlapping collections.
- Current metrics appear on the dashboard.
- Trends appear after multiple snapshots exist.
- Failed collection attempts are handled visibly and safely.
- No secrets or personal directory data are stored.
- Automated tests pass.
- A Release build succeeds.
- The README explains how another developer can run the project.

At the end, provide:

- A concise summary of the implementation.
- The important files created or changed.
- Build and test results.
- Any assumptions made.
- The next recommended step.
- A list of actions that still require tenant or Azure administrator approval.