using Microsoft.Extensions.Diagnostics.HealthChecks;
using Npgsql;

namespace Wms.Host.AppApi;

/// <summary>Readiness: the database answers. Used by the ALB target group health check.</summary>
internal sealed class PostgresHealthCheck(NpgsqlDataSource dataSource) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            await using var command = dataSource.CreateCommand("SELECT 1");
            await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            return HealthCheckResult.Healthy();
        }
        catch (Exception ex) when (ex is NpgsqlException or TimeoutException)
        {
            return HealthCheckResult.Unhealthy("PostgreSQL is unreachable.", ex);
        }
    }
}
