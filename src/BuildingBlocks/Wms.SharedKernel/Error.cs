namespace Wms.SharedKernel;

/// <summary>Category of a failure. Endpoints map it to an HTTP status.</summary>
public enum ErrorType
{
    Validation,   // 400
    Unauthorized, // 401
    Forbidden,    // 403
    NotFound,     // 404
    Conflict,     // 409 (concurrency, duplicates)
    Rule,         // 422 (business rule violated)
}

/// <summary>
/// A business failure. Codes are stable, upper snake case, prefixed by module
/// (e.g. INB_OVER_RECEIPT) and are part of the public API contract.
/// </summary>
public sealed record Error(string Code, string Message, ErrorType Type, string? Field = null)
{
    public static Error Validation(string code, string message, string? field = null) =>
        new(code, message, ErrorType.Validation, field);

    public static Error NotFound(string code, string message) => new(code, message, ErrorType.NotFound);

    public static Error Conflict(string code, string message) => new(code, message, ErrorType.Conflict);

    public static Error Rule(string code, string message, string? field = null) =>
        new(code, message, ErrorType.Rule, field);

    public static Error Forbidden(string code, string message) => new(code, message, ErrorType.Forbidden);

    public static Error Unauthorized(string code, string message) => new(code, message, ErrorType.Unauthorized);
}
