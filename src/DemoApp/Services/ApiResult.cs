namespace DemoApp.Services;

/// <summary>
/// Represents the result of an API operation.
/// </summary>
public class ApiResult<T>
{
    public bool IsSuccess { get; private init; }
    public T? Data { get; private init; }
    public string? ErrorMessage { get; private init; }

    public static ApiResult<T> Success(T data) => new() { IsSuccess = true, Data = data };
    public static ApiResult<T> Failure(string error) => new() { IsSuccess = false, ErrorMessage = error };
}

