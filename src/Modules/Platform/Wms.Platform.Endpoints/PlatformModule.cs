using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Wms.Application.Abstractions;
using Wms.Platform.Application.SystemStatus;
using Wms.Platform.Infrastructure;
using Wms.Web.Common;

namespace Wms.Platform.Endpoints;

public sealed class PlatformModule : IModule
{
    public string Name => PlatformInfrastructure.ModuleName;

    public void Register(IServiceCollection services, IConfiguration configuration) =>
        services.AddPlatformInfrastructure();

    public void MapEndpoints(IEndpointRouteBuilder app)
    {
        var system = app.MapGroup("/api/system").WithTags("System");

        system.MapGet("/status", static async (
                IQueryHandler<GetSystemStatus, SystemStatusResult> handler,
                CancellationToken ct) =>
            (await handler.Handle(new GetSystemStatus(), ct).ConfigureAwait(false)).ToHttpResult())
            .WithName("GetSystemStatus");
    }
}
