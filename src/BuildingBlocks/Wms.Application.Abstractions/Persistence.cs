namespace Wms.Application.Abstractions;

/// <summary>
/// One connection and at most one transaction per command, shared by every module
/// the command touches (LLD 2.5). Decorators open and commit it; handlers do not.
/// </summary>
public interface IUnitOfWork
{
    bool HasTransaction { get; }

    /// <summary>Opens the connection and sets the tenant session for row-level security.</summary>
    Task OpenAsync(CancellationToken ct);

    Task BeginAsync(CancellationToken ct);

    Task CommitAsync(CancellationToken ct);

    Task RollbackAsync(CancellationToken ct);
}

/// <summary>An event other modules or external systems may react to.</summary>
public interface IIntegrationEvent
{
    Guid EventId { get; }

    /// <summary>Versioned type name, e.g. "outbound.order_shipped.v1".</summary>
    string Type { get; }

    DateTimeOffset OccurredAt { get; }
}

/// <summary>Writes events to the outbox inside the current transaction.</summary>
public interface IEventPublisher
{
    Task EnqueueAsync(IIntegrationEvent integrationEvent, CancellationToken ct);
}
