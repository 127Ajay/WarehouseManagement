using Wms.SharedKernel;

namespace Wms.Application.Abstractions;

/// <summary>A request that changes state. Runs inside one database transaction.</summary>
public interface ICommand<TResult>;

/// <summary>A request that only reads. Never opens a write transaction.</summary>
public interface IQuery<TResult>;

public interface ICommandHandler<in TCommand, TResult>
    where TCommand : ICommand<TResult>
{
    Task<Result<TResult>> Handle(TCommand command, CancellationToken ct);
}

public interface IQueryHandler<in TQuery, TResult>
    where TQuery : IQuery<TResult>
{
    Task<Result<TResult>> Handle(TQuery query, CancellationToken ct);
}
