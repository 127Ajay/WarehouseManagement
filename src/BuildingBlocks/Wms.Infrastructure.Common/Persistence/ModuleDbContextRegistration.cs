using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;

namespace Wms.Infrastructure.Common.Persistence;

public static class ModuleDbContextRegistration
{
    /// <summary>
    /// Registers a module DbContext on the scope's shared connection (LLD 2.5).
    /// EF never starts its own transaction: the UnitOfWork owns it, and Npgsql runs every
    /// command on the connection inside the open transaction. Schema is created by SQL
    /// migrations, not EF migrations (TQ-11).
    /// </summary>
    public static IServiceCollection AddModuleDbContext<TContext>(this IServiceCollection services)
        where TContext : DbContext
    {
        services.AddScoped(sp =>
        {
            var unitOfWork = sp.GetRequiredService<UnitOfWork>();
            var options = new DbContextOptionsBuilder<TContext>()
                .UseNpgsql(unitOfWork.Connection)
                .AddInterceptors(sp.GetRequiredService<TenantSessionInterceptor>())
                .Options;

            var context = ActivatorUtilities.CreateInstance<TContext>(sp, options);
            context.Database.AutoTransactionBehavior = AutoTransactionBehavior.Never;
            return context;
        });

        return services;
    }
}
