using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Wms.Application.Abstractions;

namespace Wms.Web.Common;

public static class DependencyInjection
{
    /// <summary>HTTP-specific services shared by the App API and Integration API hosts.</summary>
    public static IServiceCollection AddWmsWeb(this IServiceCollection services)
    {
        services.AddHttpContextAccessor();
        services.AddScoped<IUserContext, HttpUserContext>();

        services.AddProblemDetails(options => options.CustomizeProblemDetails = context =>
            context.ProblemDetails.Extensions["traceId"] = Activity.Current?.Id ?? context.HttpContext.TraceIdentifier);
        services.AddExceptionHandler<GlobalExceptionHandler>();

        return services;
    }
}
