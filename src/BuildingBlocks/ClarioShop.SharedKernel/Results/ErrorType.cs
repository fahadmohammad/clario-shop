namespace ClarioShop.SharedKernel.Results;

public enum ErrorType
{
    Failure,
    Validation,
    NotFound,
    Conflict,
    Unauthorized,
    Forbidden,
    TooManyRequests,

    /// <summary>A service the operation depends on (e.g. a payment gateway) did not answer or failed.</summary>
    Unavailable,
}
