namespace ClarioShop.SharedKernel.Results;

public sealed class Result<TValue> : Result
{
    private readonly TValue? value;

    internal Result(TValue? value, bool isSuccess, Error error)
        : base(isSuccess, error) => this.value = value;

    public TValue Value => IsSuccess
        ? value!
        : throw new InvalidOperationException("The value of a failed result cannot be accessed.");

    public static implicit operator Result<TValue>(TValue value) => Success(value);

    public static implicit operator Result<TValue>(Error error) => Failure<TValue>(error);
}
