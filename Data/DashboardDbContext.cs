using Microsoft.EntityFrameworkCore;

namespace _365MigrationTracker.Data;

/// <summary>
/// Entity Framework Core DbContext for the migration dashboard.
/// </summary>
public class DashboardDbContext : DbContext
{
    public DashboardDbContext(DbContextOptions<DashboardDbContext> options)
        : base(options)
    {
    }

    public DbSet<MetricSnapshot> MetricSnapshots { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configure MetricSnapshot entity
        modelBuilder.Entity<MetricSnapshot>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.Property(e => e.CapturedAtUtc)
                .IsRequired()
                .HasColumnType("TEXT");

            entity.Property(e => e.SyncedUsers)
                .IsRequired();

            entity.Property(e => e.SyncedGroups)
                .IsRequired();

            entity.Property(e => e.HybridDevices)
                .IsRequired();

            entity.Property(e => e.PendingHybridDevices)
                .IsRequired();

            entity.Property(e => e.EntraJoinedDevices)
                .IsRequired();

            entity.Property(e => e.CollectionSucceeded)
                .IsRequired();

            entity.Property(e => e.CollectionDurationMilliseconds)
                .IsRequired();

            entity.Property(e => e.ErrorMessage)
                .HasMaxLength(500);

            // Index on CapturedAtUtc for efficient queries
            entity.HasIndex(e => e.CapturedAtUtc)
                .HasDatabaseName("IX_MetricSnapshot_CapturedAtUtc");

            // Index on CollectionSucceeded for filtering
            entity.HasIndex(e => e.CollectionSucceeded)
                .HasDatabaseName("IX_MetricSnapshot_CollectionSucceeded");
        });
    }
}

