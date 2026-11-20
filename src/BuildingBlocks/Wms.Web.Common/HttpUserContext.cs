using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Wms.Application.Abstractions;

namespace Wms.Web.Common;

/// <summary>Reads the caller from the request's claims. Claim names are fixed by the Identity module.</summary>
internal sealed class HttpUserContext(IHttpContextAccessor accessor) : IUserContext
{
    public const string UserIdClaim = "sub";
    public const string DeviceIdClaim = "device_id";
    public const string WarehouseIdClaim = "warehouse_id";

    private ClaimsPrincipal? Principal => accessor.HttpContext?.User;

    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated == true;

    public Guid UserId => ReadGuid(UserIdClaim)
        ?? throw new InvalidOperationException("No authenticated user in this request.");

    public Guid? DeviceId => ReadGuid(DeviceIdClaim);

    public Guid? WarehouseId => ReadGuid(WarehouseIdClaim);

    private Guid? ReadGuid(string claimType) =>
        Guid.TryParse(Principal?.FindFirstValue(claimType), out var value) ? value : null;
}
