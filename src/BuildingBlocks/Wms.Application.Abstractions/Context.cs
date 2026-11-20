namespace Wms.Application.Abstractions;

/// <summary>
/// The tenant for the current request or message. Set once by tenant resolution
/// (HTTP middleware or message consumer). Handlers never take a tenant ID from input.
/// </summary>
public interface ITenantContext
{
    bool IsResolved { get; }

    /// <summary>Throws if not resolved.</summary>
    Guid TenantId { get; }

    string PlanCode { get; }

    /// <summary>Which database the tenant lives in: shared cluster or a dedicated one.</summary>
    string ConnectionKey { get; }
}

/// <summary>The caller. Scope checks (warehouse, client, zone) build on this.</summary>
public interface IUserContext
{
    bool IsAuthenticated { get; }

    Guid UserId { get; }

    Guid? DeviceId { get; }

    Guid? WarehouseId { get; }
}

/// <summary>The only allowed source of "now". DateTime.Now is banned by an architecture test.</summary>
public interface IClock
{
    DateTimeOffset UtcNow { get; }
}
