using Microsoft.AspNetCore.Http;
using Wms.SharedKernel;

namespace Wms.Web.Common;

/// <summary>Maps results to HTTP. Errors become RFC 9457 Problem Details with our code and field.</summary>
public static class ResultHttpExtensions
{
    public static IResult ToHttpResult<T>(this Result<T> result, Func<T, IResult>? onSuccess = null) =>
        result.IsSuccess
            ? onSuccess?.Invoke(result.Value) ?? TypedResults.Ok(result.Value)
            : result.Error.ToProblem();

    public static IResult ToProblem(this Error error)
    {
        ArgumentNullException.ThrowIfNull(error);

        var extensions = new Dictionary<string, object?> { ["code"] = error.Code };
        if (error.Field is not null)
        {
            extensions["field"] = error.Field;
        }

        return TypedResults.Problem(
            title: error.Message,
            statusCode: StatusFor(error.Type),
            extensions: extensions);
    }

    public static int StatusFor(ErrorType type) => type switch
    {
        ErrorType.Validation => StatusCodes.Status400BadRequest,
        ErrorType.Unauthorized => StatusCodes.Status401Unauthorized,
        ErrorType.Forbidden => StatusCodes.Status403Forbidden,
        ErrorType.NotFound => StatusCodes.Status404NotFound,
        ErrorType.Conflict => StatusCodes.Status409Conflict,
        ErrorType.Rule => StatusCodes.Status422UnprocessableEntity,
        _ => StatusCodes.Status500InternalServerError,
    };
}
