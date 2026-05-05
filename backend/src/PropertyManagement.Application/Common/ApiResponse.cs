namespace PropertyManagement.Application.Common;

/// <summary>
/// Standard envelope for HTTP responses on the PMS module (and others).
/// Always returns { success, message, data, errors } so client code can branch on
/// success without inspecting status codes for non-2xx envelopes.
/// </summary>
public class ApiResponse<T>
{
    public bool Success { get; init; }
    public string Message { get; init; } = string.Empty;
    public T? Data { get; init; }
    public IReadOnlyList<string>? Errors { get; init; }

    public static ApiResponse<T> Ok(T data, string message = "OK") =>
        new() { Success = true, Message = message, Data = data };

    public static ApiResponse<T> Fail(string message, IReadOnlyList<string>? errors = null) =>
        new() { Success = false, Message = message, Errors = errors };
}

public class ApiResponse
{
    public bool Success { get; init; }
    public string Message { get; init; } = string.Empty;
    public IReadOnlyList<string>? Errors { get; init; }

    public static ApiResponse Ok(string message = "OK") => new() { Success = true, Message = message };
    public static ApiResponse Fail(string message, IReadOnlyList<string>? errors = null) =>
        new() { Success = false, Message = message, Errors = errors };
}
