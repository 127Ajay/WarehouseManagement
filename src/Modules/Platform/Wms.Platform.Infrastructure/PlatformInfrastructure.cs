using Microsoft.Extensions.DependencyInjection;
using Wms.Infrastructure.Common;
using Wms.Infrastructure.Common.Migrations;

namespace Wms.Platform.Infrastructure;

public static class PlatformInfrastructure
{
    public const string ModuleName = "platform";

    public static MigrationSource Migrations { get; } = new(ModuleName, typeof(PlatformInfrastructure).Assembly);

    public static IServiceCollection AddPlatformInfrastructure(this IServiceCollection services)
    {
        // Handlers live in Application (commands, domain logic) and Infrastructure (SQL read side).
        services.AddHandlersFrom(typeof(Application.SystemStatus.GetSystemStatus).Assembly);
        services.AddHandlersFrom(typeof(PlatformInfrastructure).Assembly);
        return services;
    }
}
