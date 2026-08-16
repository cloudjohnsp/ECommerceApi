namespace ECommerce.Shared.Results;

public record Result
{
    protected Result(bool isSuccess, IReadOnlyCollection<string> errors)
    {
        IsSuccess = isSuccess;
        Errors = errors;
    }

    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;
    public IReadOnlyCollection<string> Errors { get; }

    public static Result Success() => new(true, []);
    public static Result Failure(params string[] errors) => new(false, errors);
}

public sealed record Result<T> : Result
{
    private Result(T value) : base(true, []) => Value = value;

    private Result(IReadOnlyCollection<string> errors) : base(false, errors) => Value = default;

    public T? Value { get; }

    public static Result<T> Success(T value) => new(value);
    public static new Result<T> Failure(params string[] errors) => new(errors);

}
