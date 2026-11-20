using Npgsql;
using Wms.Infrastructure.Common;
using Wms.Infrastructure.Common.Migrations;
using Wms.Platform.Infrastructure;

// Applies SQL migrations for every module, in this order. Runs as a one-off task before deploy.
// Connection must use the schema owner role (wms_owner), never the runtime role.
MigrationSource[] sources =
[
    DependencyInjection.CoreMigrations,
    PlatformInfrastructure.Migrations,
];

var connectionString = Environment.GetEnvironmentVariable("WMS_MIGRATOR_CONNECTION")
    ?? "Host=localhost;Port=5432;Database=wms;Username=wms_owner;Password=wms_owner_dev";

try
{
    await using var dataSource = NpgsqlDataSource.Create(connectionString);
    using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(10));
    var runner = new SqlMigrationRunner(dataSource, Console.WriteLine);
    await runner.RunAsync(sources, timeout.Token).ConfigureAwait(false);
    return 0;
}
catch (Exception ex) when (ex is NpgsqlException or InvalidOperationException or OperationCanceledException)
{
    await Console.Error.WriteLineAsync($"Migration failed: {ex.Message}").ConfigureAwait(false);
    return 1;
}
