namespace TechYouthBpm.Application.Common;

public class Result<T>
{
    private Result(bool isSuccess, T? value, IReadOnlyList<string> errors)
    {
        IsSuccess = isSuccess;
        Value = value;
        Errors = errors;
    }

    public bool IsSuccess { get; }
    public T? Value { get; }
    public IReadOnlyList<string> Errors { get; }

    public static Result<T> Success(T value) => new(true, value, []);
    public static Result<T> Failure(params string[] errors) => new(false, default, errors);
    public static Result<T> Failure(IEnumerable<string> errors) => new(false, default, errors.ToArray());
}
