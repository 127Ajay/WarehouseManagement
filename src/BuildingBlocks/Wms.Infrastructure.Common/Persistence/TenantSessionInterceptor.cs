using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Wms.Application.Abstractions;

namespace Wms.Infrastructure.Common.Persistence;

/// <summary>
/// Covers the case where EF Core opens the shared connection itself (a query run
/// outside the decorators). The tenant session is still set before any SQL runs.
/// </summary>
public sealed class TenantSessionInterceptor(ITenantContext tenant) : DbConnectionInterceptor
{
    public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData) =>
        TenantSession.Apply(connection, tenant);

    public override Task ConnectionOpenedAsync(
        DbConnection connection,
        ConnectionEndEventData eventData,
        CancellationToken cancellationToken = default) =>
        TenantSession.ApplyAsync(connection, tenant, cancellationToken);
}
