using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Wms.Web.Common;

/// <summary>
/// Last line of defence for unexpected exceptions. Never leaks exception details to the caller;
/// the traceId in the response links to the full error in logs.
/// </summary>
internal sealed partial class GlobalExceptionHandler(
    IProblemDetailsService problemDetails,
    ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is OperationCanceledException && httpContext.RequestAborted.IsCancellationRequested)
        {
            return true; // client went away; nothing to send
        }

        LogUnhandled(logger, exception, httpContext.Request.Path);

        httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "Something went wrong. Quote the traceId to support.",
                Extensions = { ["code"] = "INTERNAL_ERROR" },
            },
        }).ConfigureAwait(false);
    }

    [LoggerMessage(EventId = 500, Level = LogLevel.Error, Message = "Unhandled exception on {Path}")]
    private static partial void LogUnhandled(ILogger logger, Exception exception, string path);
}
