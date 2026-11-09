namespace RaceDay.Web.Services;

/// <summary>Outcome of an API call: either a value, or a friendly error message plus the HTTP status.</summary>
public class ApiResult<T>
{
    public bool IsSuccess { get; private init; }
    public int StatusCode { get; private init; }
    public T? Value { get; private init; }
    public string? Error { get; private init; }

    public bool IsNotFound => StatusCode == 404;
    public bool IsConflict => StatusCode == 409;

    public static ApiResult<T> Success(T value, int statusCode) =>
        new() { IsSuccess = true, Value = value, StatusCode = statusCode };

    public static ApiResult<T> Failure(int statusCode, string error) =>
        new() { IsSuccess = false, StatusCode = statusCode, Error = error };
}

/// <summary>A file (image) returned by the API.</summary>
public record ApiFile(byte[] Bytes, string ContentType);
//ST10486532
//Author: Gift Tshwaane