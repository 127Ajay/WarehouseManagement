using System.Text.Json;
using Npgsql;
using NpgsqlTypes;
using Wms.Application.Abstractions;
using Wms.Infrastructure.Common.Persistence;

namespace Wms.Infrastructure.Common.Messaging;

/// <summary>
/// Transactional outbox (LLD 2.7). The event row commits or rolls back with the business change.
/// The Worker publishes it to the transport later.
/// </summary>
internal sealed class OutboxEventPublisher(UnitOfWork unitOfWork, ITenantContext tenant) : IEventPublisher
{
    private const string Sql = """
        INSERT INTO messaging.outbox_message (id, tenant_id, type, payload, occurred_at)
        VALUES (@id, @tenant_id, @type, @payload, @occurred_at)
        """;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task EnqueueAsync(IIntegrationEvent integrationEvent, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        if (!unitOfWork.HasTransaction)
        {
            throw new InvalidOperationException("Integration events can only be enqueued inside a command transaction.");
        }

        var payload = JsonSerializer.Serialize(integrationEvent, integrationEvent.GetType(), JsonOptions);

        await using var command = new NpgsqlCommand(Sql, unitOfWork.Connection, unitOfWork.Transaction);
        command.Parameters.AddWithValue("id", integrationEvent.EventId);
        command.Parameters.AddWithValue("tenant_id", tenant.IsResolved ? tenant.TenantId : (object)DBNull.Value);
        command.Parameters.AddWithValue("type", integrationEvent.Type);
        command.Parameters.Add(new NpgsqlParameter("payload", NpgsqlDbType.Jsonb) { Value = payload });
        command.Parameters.AddWithValue("occurred_at", integrationEvent.OccurredAt);
        await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }
}
