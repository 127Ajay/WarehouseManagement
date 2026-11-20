using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Wms.Application.Abstractions;
using Wms.SharedKernel;

namespace Wms.Infrastructure.Common.Persistence;

/// <summary>
/// Base for every module DbContext (layer 2 of tenant isolation, HLD 6.2):
/// a global query filter on TenantId for tenant-owned entities, and TenantId stamped on insert.
/// </summary>
public abstract class TenantDbContext : DbContext
{
    private static readonly MethodInfo ApplyFilterMethod = typeof(TenantDbContext)
        .GetMethod(nameof(ApplyTenantFilter), BindingFlags.NonPublic | BindingFlags.Instance)!;

    private readonly ITenantContext _tenant;

    protected TenantDbContext(DbContextOptions options, ITenantContext tenant)
        : base(options)
    {
        _tenant = tenant;
    }

    /// <summary>Read by EF at query time, so each query uses the current scope's tenant.</summary>
    protected Guid CurrentTenantId => _tenant.TenantId;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        base.OnModelCreating(modelBuilder);

        foreach (var entityType in modelBuilder.Model.GetEntityTypes()
                     .Where(t => t.BaseType is null && typeof(ITenantOwned).IsAssignableFrom(t.ClrType))
                     .ToList())
        {
            ApplyFilterMethod.MakeGenericMethod(entityType.ClrType).Invoke(this, [modelBuilder]);
        }
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        StampTenant();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        StampTenant();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    private void ApplyTenantFilter<TEntity>(ModelBuilder modelBuilder)
        where TEntity : class, ITenantOwned =>
        modelBuilder.Entity<TEntity>().HasQueryFilter(e => e.TenantId == CurrentTenantId);

    private void StampTenant()
    {
        foreach (var entry in ChangeTracker.Entries<ITenantOwned>())
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.TenantId = _tenant.TenantId;
            }
            else if ((entry.State is EntityState.Modified or EntityState.Deleted)
                     && entry.Entity.TenantId != _tenant.TenantId)
            {
                throw new InvalidOperationException("Attempt to change data of another tenant.");
            }
        }
    }
}
