using System.Data.Common;
using Wms.Application.Abstractions;

namespace Wms.Infrastructure.Common.Persistence;

/// <summary>
/// Sets the PostgreSQL session variable that row-level security policies read
/// (layer 3 of tenant isolation, HLD 6.2). Runs on every connection open.
/// Npgsql resets session state when a connection returns to the pool,
/// so a pooled connection never carries another tenant's value.
/// </summary>
internal static class TenantSession
{
    private const string Sql = "SELECT set_config('app.tenant_id', @tenant, false)";

    public static async Task ApplyAsync(DbConnection connection, ITenantContext tenant, CancellationToken ct)
    {
        await using var command = Create(connection, tenant);
        await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    public static void Apply(DbConnection connection, ITenantContext tenant)
    {
        using var command = Create(connection, tenant);
        command.ExecuteNonQuery();
    }

    private static DbCommand Create(DbConnection connection, ITenantContext tenant)
    {
        var command = connection.CreateCommand();
        command.CommandText = Sql;
        var parameter = command.CreateParameter();
        parameter.ParameterName = "tenant";
        // Empty string when no tenant: policies use NULLIF(..., '') so nothing tenant-owned matches.
        parameter.Value = tenant.IsResolved ? tenant.TenantId.ToString() : string.Empty;
        command.Parameters.Add(parameter);
        return command;
    }
}
