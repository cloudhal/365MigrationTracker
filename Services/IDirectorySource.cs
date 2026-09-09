using _365MigrationTracker.Models;

namespace _365MigrationTracker.Services;

/// <summary>
/// Supplies the individual directory objects behind the dashboard's counts, for the
/// drill-down grids. Separate from IMigrationMetricsSource, which only ever deals in
/// aggregate counts and is what gets persisted.
/// </summary>
public interface IDirectorySource
{
    Task<IReadOnlyList<DirectoryUser>> GetUsersAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DirectoryGroup>> GetGroupsAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DirectoryDevice>> GetDevicesAsync(CancellationToken cancellationToken = default);
}
