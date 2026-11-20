using System.Data;
using Npgsql;
using Wms.Application.Abstractions;

namespace Wms.Infrastructure.Common.Persistence;

/// <summary>
/// One connection and at most one transaction per scope (LLD 2.5, TQ-07).
/// The connection object exists from the start so module DbContexts can be built on it
/// at injection time; it is opened asynchronously by the decorators before a handler runs.
/// </summary>
public sealed class UnitOfWork : IUnitOfWork, IAsyncDisposable
{
    private readonly ITenantContext _tenant;
    private NpgsqlTransaction? _transaction;

    public UnitOfWork(NpgsqlDataSource dataSource, ITenantContext tenant)
    {
        ArgumentNullException.ThrowIfNull(dataSource);
        _tenant = tenant;
        Connection = dataSource.CreateConnection();
    }

    public NpgsqlConnection Connection { get; }

    public NpgsqlTransaction? Transaction => _transaction;

    public bool HasTransaction => _transaction is not null;

    public async Task OpenAsync(CancellationToken ct)
    {
        if (Connection.State == ConnectionState.Open)
        {
            return;
        }

        await Connection.OpenAsync(ct).ConfigureAwait(false);
        await TenantSession.ApplyAsync(Connection, _tenant, ct).ConfigureAwait(false);
    }

    public async Task BeginAsync(CancellationToken ct)
    {
        if (_transaction is not null)
        {
            return;
        }

        await OpenAsync(ct).ConfigureAwait(false);
        _transaction = await Connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct).ConfigureAwait(false);
    }

    public async Task CommitAsync(CancellationToken ct)
    {
        if (_transaction is null)
        {
            throw new InvalidOperationException("No transaction to commit.");
        }

        await _transaction.CommitAsync(ct).ConfigureAwait(false);
        await _transaction.DisposeAsync().ConfigureAwait(false);
        _transaction = null;
    }

    public async Task RollbackAsync(CancellationToken ct)
    {
        if (_transaction is null)
        {
            return;
        }

        await _transaction.RollbackAsync(ct).ConfigureAwait(false);
        await _transaction.DisposeAsync().ConfigureAwait(false);
        _transaction = null;
    }

    public async ValueTask DisposeAsync()
    {
        if (_transaction is not null)
        {
            await _transaction.DisposeAsync().ConfigureAwait(false);
        }

        await Connection.DisposeAsync().ConfigureAwait(false);
    }
}
