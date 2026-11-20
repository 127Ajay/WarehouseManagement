using System.Diagnostics;
using FluentValidation;
using Microsoft.Extensions.Logging;
using Wms.Application.Abstractions;
using Wms.SharedKernel;

namespace Wms.Infrastructure.Common.Pipeline;

// Command pipeline: Validation > Transaction > Logging > Handler.
// Query pipeline:   QueryScope > Logging > Handler.

internal sealed class ValidationDecorator<TCommand, TResult>(
    ICommandHandler<TCommand, TResult> inner,
    IEnumerable<IValidator<TCommand>> validators)
    : ICommandHandler<TCommand, TResult>
    where TCommand : ICommand<TResult>
{
    private const string DefaultCode = "VALIDATION_FAILED";

    public async Task<Result<TResult>> Handle(TCommand command, CancellationToken ct)
    {
        var validatorList = validators as IReadOnlyCollection<IValidator<TCommand>> ?? validators.ToList();
        if (validatorList.Count == 0)
        {
            return await inner.Handle(command, ct).ConfigureAwait(false);
        }

        var context = new ValidationContext<TCommand>(command);
        foreach (var validator in validatorList)
        {
            var result = await validator.ValidateAsync(context, ct).ConfigureAwait(false);
            var failure = result.Errors.FirstOrDefault();
            if (failure is not null)
            {
                // Validators set business codes with .WithErrorCode("MOD_CODE"); built-in codes end in "Validator".
                var code = string.IsNullOrEmpty(failure.ErrorCode) || failure.ErrorCode.EndsWith("Validator", StringComparison.Ordinal)
                    ? DefaultCode
                    : failure.ErrorCode;
                return Error.Validation(code, failure.ErrorMessage, failure.PropertyName);
            }
        }

        return await inner.Handle(command, ct).ConfigureAwait(false);
    }
}

internal sealed class TransactionDecorator<TCommand, TResult>(
    ICommandHandler<TCommand, TResult> inner,
    IUnitOfWork unitOfWork)
    : ICommandHandler<TCommand, TResult>
    where TCommand : ICommand<TResult>
{
    public async Task<Result<TResult>> Handle(TCommand command, CancellationToken ct)
    {
        // A command called from inside another command joins the outer transaction.
        if (unitOfWork.HasTransaction)
        {
            return await inner.Handle(command, ct).ConfigureAwait(false);
        }

        await unitOfWork.BeginAsync(ct).ConfigureAwait(false);
        try
        {
            var result = await inner.Handle(command, ct).ConfigureAwait(false);
            if (result.IsSuccess)
            {
                await unitOfWork.CommitAsync(ct).ConfigureAwait(false);
            }
            else
            {
                await unitOfWork.RollbackAsync(ct).ConfigureAwait(false);
            }

            return result;
        }
        catch
        {
            await unitOfWork.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
            throw;
        }
    }
}

internal sealed class QueryScopeDecorator<TQuery, TResult>(
    IQueryHandler<TQuery, TResult> inner,
    IUnitOfWork unitOfWork)
    : IQueryHandler<TQuery, TResult>
    where TQuery : IQuery<TResult>
{
    public async Task<Result<TResult>> Handle(TQuery query, CancellationToken ct)
    {
        await unitOfWork.OpenAsync(ct).ConfigureAwait(false);
        return await inner.Handle(query, ct).ConfigureAwait(false);
    }
}

internal sealed class LoggingCommandDecorator<TCommand, TResult>(
    ICommandHandler<TCommand, TResult> inner,
    ILogger<TCommand> logger)
    : ICommandHandler<TCommand, TResult>
    where TCommand : ICommand<TResult>
{
    public async Task<Result<TResult>> Handle(TCommand command, CancellationToken ct)
    {
        var started = Stopwatch.GetTimestamp();
        var result = await inner.Handle(command, ct).ConfigureAwait(false);
        PipelineLog.Handled(logger, typeof(TCommand).Name, Stopwatch.GetElapsedTime(started).TotalMilliseconds, result.Error?.Code ?? "OK");
        return result;
    }
}

internal sealed class LoggingQueryDecorator<TQuery, TResult>(
    IQueryHandler<TQuery, TResult> inner,
    ILogger<TQuery> logger)
    : IQueryHandler<TQuery, TResult>
    where TQuery : IQuery<TResult>
{
    public async Task<Result<TResult>> Handle(TQuery query, CancellationToken ct)
    {
        var started = Stopwatch.GetTimestamp();
        var result = await inner.Handle(query, ct).ConfigureAwait(false);
        PipelineLog.Handled(logger, typeof(TQuery).Name, Stopwatch.GetElapsedTime(started).TotalMilliseconds, result.Error?.Code ?? "OK");
        return result;
    }
}

internal static class PipelineLog
{
    private static readonly Action<ILogger, string, double, string, Exception?> HandledMessage =
        LoggerMessage.Define<string, double, string>(
            LogLevel.Information,
            new EventId(1000, "RequestHandled"),
            "Handled {Request} in {ElapsedMs:0.0} ms with outcome {Outcome}");

    public static void Handled(ILogger logger, string request, double elapsedMs, string outcome) =>
        HandledMessage(logger, request, elapsedMs, outcome, null);
}
