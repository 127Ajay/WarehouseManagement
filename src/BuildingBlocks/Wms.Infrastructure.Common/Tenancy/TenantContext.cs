using Wms.Application.Abstractions;

namespace Wms.Infrastructure.Common.Tenancy;

/// <summary>
/// Scoped holder for the current tenant. Resolved once per request (HTTP middleware)
/// or per message (consumer), then read-only for the rest of the scope.
/// </summary>
public sealed class TenantContext : ITenantContext
{
    private Guid? _tenantId;

    public bool IsResolved => _tenantId.HasValue;

    public Guid TenantId => _tenantId ?? throw new InvalidOperationException(
        "Tenant is not resolved for this scope. Tenant-owned data cannot be accessed.");

    public string PlanCode { get; private set; } = string.Empty;

    public string ConnectionKey { get; private set; } = string.Empty;

    public void Resolve(Guid tenantId, string planCode, string connectionKey)
    {
        if (_tenantId.HasValue && _tenantId.Value != tenantId)
        {
            throw new InvalidOperationException("Tenant is already resolved for this scope and cannot change.");
        }

        _tenantId = tenantId;
        PlanCode = planCode;
        ConnectionKey = connectionKey;
    }
}
