using Wms.Application.Abstractions;

namespace Wms.Platform.Application.SystemStatus;

/// <summary>
/// Smoke query that exercises the full read pipeline: QueryScope (opens the connection
/// and sets the tenant session) > Logging > Handler. Used by the EN-01 demo and integration tests.
/// </summary>
public sealed record GetSystemStatus : IQuery<SystemStatusResult>;

public sealed record SystemStatusResult(
    DateTimeOffset ApplicationTimeUtc,
    DateTimeOffset DatabaseTimeUtc,
    string DatabaseUser,
    string? TenantSession);
