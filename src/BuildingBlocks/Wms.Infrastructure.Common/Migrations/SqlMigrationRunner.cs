using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Npgsql;

namespace Wms.Infrastructure.Common.Migrations;

/// <summary>A module's migration scripts: embedded resources named V0001__description.sql.</summary>
public sealed record MigrationSource(string Module, Assembly Assembly);

/// <summary>
/// Applies versioned SQL scripts per module (TQ-11: SQL-first migrations, because
/// row-level security policies, partitions and grants are SQL anyway).
/// Each script runs in its own transaction. Applied scripts are recorded with a checksum;
/// an edited script that was already applied stops the run.
/// </summary>
public sealed partial class SqlMigrationRunner(NpgsqlDataSource ownerDataSource, Action<string> log)
{
    // Any constant works; all migrator instances must use the same one.
    private const long AdvisoryLockKey = 7_423_001;

    public async Task<int> RunAsync(IReadOnlyList<MigrationSource> sources, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(sources);

        await using var connection = await ownerDataSource.OpenConnectionAsync(ct).ConfigureAwait(false);
        await ExecuteAsync(connection, null, "SELECT pg_advisory_lock(" + AdvisoryLockKey + ")", ct).ConfigureAwait(false);

        try
        {
            await ExecuteAsync(connection, null, """
                CREATE TABLE IF NOT EXISTS public.schema_migration (
                  module     varchar(50)  NOT NULL,
                  version    integer      NOT NULL,
                  name       varchar(200) NOT NULL,
                  checksum   char(64)     NOT NULL,
                  applied_at timestamptz  NOT NULL DEFAULT now(),
                  PRIMARY KEY (module, version)
                )
                """, ct).ConfigureAwait(false);

            var applied = 0;
            foreach (var source in sources)
            {
                var done = await LoadAppliedAsync(connection, source.Module, ct).ConfigureAwait(false);
                foreach (var script in Discover(source))
                {
                    if (done.TryGetValue(script.Version, out var checksum))
                    {
                        if (!string.Equals(checksum, script.Checksum, StringComparison.Ordinal))
                        {
                            throw new InvalidOperationException(
                                $"Migration {source.Module} V{script.Version:D4} was changed after it was applied. Add a new script instead.");
                        }

                        continue;
                    }

                    await ApplyAsync(connection, source.Module, script, ct).ConfigureAwait(false);
                    log($"Applied {source.Module} V{script.Version:D4} {script.Name}");
                    applied++;
                }
            }

            log(applied == 0 ? "Database is up to date." : $"Applied {applied} migration(s).");
            return applied;
        }
        finally
        {
            await ExecuteAsync(connection, null, "SELECT pg_advisory_unlock(" + AdvisoryLockKey + ")", CancellationToken.None)
                .ConfigureAwait(false);
        }
    }

    internal static IReadOnlyList<MigrationScript> Discover(MigrationSource source)
    {
        var scripts = new List<MigrationScript>();
        foreach (var resource in source.Assembly.GetManifestResourceNames())
        {
            var match = ScriptName().Match(resource);
            if (!match.Success)
            {
                continue;
            }

            using var stream = source.Assembly.GetManifestResourceStream(resource)!;
            using var reader = new StreamReader(stream, Encoding.UTF8);
            var sql = reader.ReadToEnd();
            var version = int.Parse(match.Groups["version"].Value, System.Globalization.CultureInfo.InvariantCulture);
            var checksum = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(sql)));
            scripts.Add(new MigrationScript(version, match.Groups["name"].Value, sql, checksum));
        }

        var duplicate = scripts.GroupBy(s => s.Version).FirstOrDefault(g => g.Count() > 1);
        if (duplicate is not null)
        {
            throw new InvalidOperationException($"Module {source.Module} has two scripts with version {duplicate.Key}.");
        }

        return [.. scripts.OrderBy(s => s.Version)];
    }

    private static async Task<Dictionary<int, string>> LoadAppliedAsync(NpgsqlConnection connection, string module, CancellationToken ct)
    {
        var result = new Dictionary<int, string>();
        await using var command = new NpgsqlCommand("SELECT version, checksum FROM public.schema_migration WHERE module = @module", connection);
        command.Parameters.AddWithValue("module", module);
        await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            result[reader.GetInt32(0)] = reader.GetString(1);
        }

        return result;
    }

    private static async Task ApplyAsync(NpgsqlConnection connection, string module, MigrationScript script, CancellationToken ct)
    {
        await using var transaction = await connection.BeginTransactionAsync(ct).ConfigureAwait(false);
        await ExecuteAsync(connection, transaction, script.Sql, ct).ConfigureAwait(false);

        await using var record = new NpgsqlCommand(
            "INSERT INTO public.schema_migration (module, version, name, checksum) VALUES (@module, @version, @name, @checksum)",
            connection,
            transaction);
        record.Parameters.AddWithValue("module", module);
        record.Parameters.AddWithValue("version", script.Version);
        record.Parameters.AddWithValue("name", script.Name);
        record.Parameters.AddWithValue("checksum", script.Checksum);
        await record.ExecuteNonQueryAsync(ct).ConfigureAwait(false);

        await transaction.CommitAsync(ct).ConfigureAwait(false);
    }

    private static async Task ExecuteAsync(NpgsqlConnection connection, NpgsqlTransaction? transaction, string sql, CancellationToken ct)
    {
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    [GeneratedRegex(@"\.Migrations\.V(?<version>\d{4})__(?<name>[A-Za-z0-9_]+)\.sql$")]
    private static partial Regex ScriptName();
}

internal sealed record MigrationScript(int Version, string Name, string Sql, string Checksum);
