using Npgsql;
using Wms.Application.Abstractions;
using Wms.Infrastructure.Common.Persistence;
using Wms.Platform.Application.SystemStatus;
using Wms.SharedKernel;

namespace Wms.Platform.Infrastructure.SystemStatus;

/// <summary>Read-side handler: plain SQL on the scope's shared connection.</summary>
internal sealed class GetSystemStatusHandler(UnitOfWork unitOfWork, IClock clock)
    : IQueryHandler<GetSystemStatus, SystemStatusResult>
{
    private const string Sql = "SELECT now(), current_user, NULLIF(current_setting('app.tenant_id', true), '')";

    public async Task<Result<SystemStatusResult>> Handle(GetSystemStatus query, CancellationToken ct)
    {
        await using var command = new NpgsqlCommand(Sql, unitOfWork.Connection);
        await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        await reader.ReadAsync(ct).ConfigureAwait(false);

        return new SystemStatusResult(
            ApplicationTimeUtc: clock.UtcNow,
            DatabaseTimeUtc: reader.GetFieldValue<DateTimeOffset>(0),
            DatabaseUser: reader.GetString(1),
            TenantSession: await reader.IsDBNullAsync(2, ct).ConfigureAwait(false) ? null : reader.GetString(2));
    }
}
