using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Wms.Web.Common;

/// <summary>
/// A feature module's entry point (LLD 2.10). A host lists the modules it composes;
/// adding a module to a host is one line.
/// </summary>
public interface IModule
{
    /// <summary>Lower-case name, also used as the migration module key and DB schema.</summary>
    string Name { get; }

    void Register(IServiceCollection services, IConfiguration configuration);

    void MapEndpoints(IEndpointRouteBuilder app);
}
