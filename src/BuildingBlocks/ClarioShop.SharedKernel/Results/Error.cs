namespace ClarioShop.SharedKernel.Results;

public sealed record Error(string Code, string Message, ErrorType Type)
{
    public static readonly Error None = new(string.Empty, string.Empty, ErrorType.Failure);

    public IReadOnlyDictionary<string, string[]>? ValidationErrors { get; init; }

    public static Error Failure(string code, string message) => new(code, message, ErrorType.Failure);

    public static Error NotFound(string code, string message) => new(code, message, ErrorType.NotFound);

    public static Error Conflict(string code, string message) => new(code, message, ErrorType.Conflict);

    public static Error Unauthorized(string code, string message) => new(code, message, ErrorType.Unauthorized);

    public static Error Forbidden(string code, string message) => new(code, message, ErrorType.Forbidden);

    public static Error TooManyRequests(string code, string message) => new(code, message, ErrorType.TooManyRequests);

    public static Error Unavailable(string code, string message) => new(code, message, ErrorType.Unavailable);

    public static Error Validation(IReadOnlyDictionary<string, string[]> errors) =>
        new("validation", "One or more validation errors occurred.", ErrorType.Validation) { ValidationErrors = errors };

    public static Error Validation(string property, string message) =>
        Validation(new Dictionary<string, string[]> { [property] = [message] });
}
