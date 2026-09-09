365 Migration Tracker · production readiness

https://claude.ai/code/artifact/5faa8bdb-eef0-4c52-bfc2-563c9d907b45

Getting the tracker ready for customers
The app works end to end against a live tenant with multi-tenant Entra sign-in. What follows is what stands between that and letting an MSP customer use it — moving to App Service and Azure SQL, and closing the gaps that only appear once someone other than you is signed in.

Snapshots
108
Tenants
1
Tests
9 passing
Uncommitted
213 files
Blocks launch
nobody else can use it until these are done

One SQLite-ism to remove while you're there: CapturedAtUtc is pinned to HasColumnType("TEXT") in DashboardDbContext. SQL Server wants datetime2, or simply no explicit type.

− Microsoft.EntityFrameworkCore.Sqlite  ·  options.UseSqlite(...)
+ Microsoft.EntityFrameworkCore.SqlServer  ·  options.UseSqlServer(...)
If skipped
SQLite on App Service lives on the Azure Files share behind /home. Its file locking is unreliable over SMB, and it cannot survive a second instance.
Distributed token cache
The token cache is currently held in process memory. This is the exact fault that left your Edge profile signed in but unable to read anything: the auth cookie outlived the cache, so MSAL had no account to acquire a Graph token for.

− .AddInMemoryTokenCaches()
+ .AddDistributedTokenCaches() · AddDistributedSqlServerCache(...)
If skipped
Every deployment and every idle-restart signs all users out of Graph. With two instances, roughly half of requests find no cached token. Users see a redirect through sign-in with no explanation of why.
Redirect URI and federated credential for the real hostname
Add https://<app>.azurewebsites.net/signin-oidc to the app registration, plus any custom domain. Turn on the App Service system-assigned managed identity, then add a federated credential on the registration pointing at it.

appsettings.Production.json already declares SignedAssertionFromManagedIdentity, so no secret is needed in production — but that path has never run against a real managed identity, only had its configuration validated.

If skipped
Sign-in fails with a reply-URL mismatch, or falls back to needing a client secret in App Service configuration.
App Service platform settings
Blazor Server holds a SignalR circuit per signed-in user, which changes what the platform needs from the defaults.

Setting	Value	Why
WebSockets	On	Circuits fall back to long-polling without it
ARR affinity	On	A circuit is bound to one instance
Always On	On	Idle unload drops every live circuit
HTTPS Only	On	Auth cookies must not travel in clear
Application settings
Both paths are already configuration-driven, so this is configuration only, not code.

AzureAd__ClientId            = <client id>
ConnectionStrings__Tracker   = <Azure SQL connection string>
DataProtection__KeyRingPath  = /home/dataprotection-keys
ASPNETCORE_ENVIRONMENT       = Production
No AzureAd__ClientSecret. That is the point of the federated credential — there is nothing to rotate or leak.

Before a second instance
correct on one instance, wrong on two
Azure SignalR Service
ARR affinity keeps a user pinned to the instance holding their circuit. It is a workaround, not a solution — it survives neither an instance restart nor a rebalance. Azure SignalR Service moves circuit state off the app instances entirely.

Data protection keys off local disk
/home is shared between instances, so the file-based key ring holds up initially. Azure Blob Storage with a Key Vault–protected key is the more durable arrangement, and removes the dependency on the file share.

The visit throttle is per-instance
SnapshotOnVisitService tracks last-collection time in a static dictionary. Two instances each keep their own, so a tenant can get up to one snapshot per instance per interval instead of one overall.

Harmless — a few extra rows — but the six-hour interval stops being exactly true. Moving the throttle into the database alongside the snapshots would fix it.

Worth doing before customers
not blocking, but you will want them
Test the tenant isolation
Every read in SqliteMetricSnapshotStore filters on TenantId, and that filter is the only thing keeping one customer's figures away from another's. The nine passing tests cover the collection service and the simulated source. None of them touch tenant filtering.

A test that writes snapshots for two tenants and asserts each only ever reads its own would be short, and it guards the one boundary that actually matters in a shared database.

Application Insights
Logs currently go to the console. Once it is someone else's tenant failing, at a time you were not watching, console output is not enough to answer why.

A retention policy
MetricSnapshots grows without limit and nothing prunes it. At four snapshots a day per tenant it is slow, but it is unbounded — and the trend chart never asks for more than ninety days.

Remove or fence off the simulated source
SimulatedMetricsSource is still registered and still selectable by setting MetricsCollection:Source. It was what produced the 150 fake rows that had to be deleted from the trend. In a shared production database, a misconfigured setting would write fabricated figures against a real customer's tenant id.

A way to delete a customer's data
When a customer offboards there is currently no path to remove their rows. It is one delete by tenant id, but it needs to exist and be findable before the first person asks.

Health check endpoint
App Service health probes need somewhere to point. A check that confirms the database is reachable is enough to distinguish a hung instance from a slow one.

Already handled
so you don't redo it
Connection string and key ring path are configuration-driven
— deployment needs no code change for either.
Federated credential configuration is written
—
appsettings.Production.json
declares it; only the Azure side remains.
Tenant column exists, is indexed, and is required on every read
— the store's read methods take
tenantId
as a mandatory argument so it cannot be forgotten.
Failed collections are recorded as failures
— no fabricated zeros written as successes, which is what corrupted the trend twice.
Re-authentication and consent are handled as redirects
— not raw
IDW10502
errors or silent zeroes.
Declined consent has its own page
— explains what the app reads and offers a retry.
Tests run
— they are in their own project now; previously they could never execute at all.
Device counts are Windows-only
— phones and tablets no longer sit in the denominator making the migration look half as advanced.