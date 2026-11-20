using System.Reflection;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Wms.Application.Abstractions;
using Wms.Infrastructure.Common.Messaging;
using Wms.Infrastructure.Common.Migrations;
using Wms.Infrastructure.Common.Persistence;
using Wms.Infrastructure.Common.Pipeline;
using Wms.Infrastructure.Common.Tenancy;
using Wms.Infrastructure.Common.Time;

namespace Wms.Infrastructure.Common;

public static class DependencyInjection
{
    /// <summary>Migrations owned by the building blocks (messaging schema).</summary>
    public static MigrationSource CoreMigrations { get; } = new("core", typeof(DependencyInjection).Assembly);

    /// <summary>Shared infrastructure for every host. Call once.</summary>
    public static IServiceCollection AddWmsInfrastructure(this IServiceCollection services, string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        services.AddSingleton(_ => NpgsqlDataSource.Create(connectionString));
        services.AddSingleton<IClock, SystemClock>();

        services.AddScoped<TenantContext>();
        services.AddScoped<ITenantContext>(sp => sp.GetRequiredService<TenantContext>());

        services.AddScoped<UnitOfWork>();
        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<UnitOfWork>());
        services.AddScoped<TenantSessionInterceptor>();
        services.AddScoped<IEventPublisher, OutboxEventPublisher>();

        return services;
    }

    /// <summary>Registers command/query handlers and validators found in a module assembly.</summary>
    public static IServiceCollection AddHandlersFrom(this IServiceCollection services, Assembly assembly)
    {
        services.Scan(scan => scan
            .FromAssemblies(assembly)
            .AddClasses(c => c.AssignableTo(typeof(ICommandHandler<,>)), publicOnly: false)
            .AsImplementedInterfaces()
            .WithScopedLifetime()
            .AddClasses(c => c.AssignableTo(typeof(IQueryHandler<,>)), publicOnly: false)
            .AsImplementedInterfaces()
            .WithScopedLifetime());

        services.AddValidatorsFromAssembly(assembly, includeInternalTypes: true);
        return services;
    }

    /// <summary>
    /// Wraps all registered handlers in the pipeline. Call after every module has registered.
    /// The last decorator applied is the outermost.
    /// </summary>
    public static IServiceCollection AddHandlerPipeline(this IServiceCollection services)
    {
        services.TryDecorate(typeof(ICommandHandler<,>), typeof(LoggingCommandDecorator<,>));
        services.TryDecorate(typeof(ICommandHandler<,>), typeof(TransactionDecorator<,>));
        services.TryDecorate(typeof(ICommandHandler<,>), typeof(ValidationDecorator<,>));

        services.TryDecorate(typeof(IQueryHandler<,>), typeof(LoggingQueryDecorator<,>));
        services.TryDecorate(typeof(IQueryHandler<,>), typeof(QueryScopeDecorator<,>));
        return services;
    }
}
