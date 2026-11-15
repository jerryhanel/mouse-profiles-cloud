namespace MouseProfilesCloud.Models;

public class ServiceResult
{
    public bool Succeeded { get; protected init; }
    public string? Error { get; protected init; }
    public int StatusCode { get; protected init; }

    public static ServiceResult Ok() => new() { Succeeded = true, StatusCode = 200 };

    public static ServiceResult Fail(string error, int statusCode = 400) =>
        new() { Succeeded = false, Error = error, StatusCode = statusCode };

    public static ServiceResult<T> Ok<T>(T value) =>
        new() { Succeeded = true, StatusCode = 200, Value = value };

    public static ServiceResult<T> Fail<T>(string error, int statusCode = 400) =>
        new() { Succeeded = false, Error = error, StatusCode = statusCode };
}

public class ServiceResult<T> : ServiceResult
{
    public T? Value { get; init; }
}
