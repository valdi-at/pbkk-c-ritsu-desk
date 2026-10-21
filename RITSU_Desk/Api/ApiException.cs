using System.Text.Json;

namespace RITSU_Desk.Api;

public enum ApiErrorKind
{
    Http,
    Network,
    Timeout,
    InvalidResponse,
}

public static class ApiErrorCodes
{
    public const string ValidationError = "VALIDATION_ERROR";
    public const string Unauthorized = "UNAUTHORIZED";
    public const string InvalidCredentials = "INVALID_CREDENTIALS";
    public const string Forbidden = "FORBIDDEN";
    public const string SelfActionForbidden = "SELF_ACTION_FORBIDDEN";
    public const string NotFound = "NOT_FOUND";
    public const string AlreadyReviewed = "ALREADY_REVIEWED";
    public const string HasDependencies = "HAS_DEPENDENCIES";
    public const string RateLimited = "RATE_LIMITED";
    public const string Internal = "INTERNAL";

    public const string Network = "NETWORK";
    public const string Timeout = "TIMEOUT";
    public const string InvalidResponse = "INVALID_RESPONSE";
    public const string UnsupportedVersion = "UNSUPPORTED_VERSION";
}

public sealed class ApiException : Exception
{
    public ApiException(ApiErrorKind kind, int? statusCode, string code, string message, JsonElement? details = null, Exception? inner = null)
        : base(message, inner)
    {
        Kind = kind;
        StatusCode = statusCode;
        Code = code;
        Details = details;
    }

    public ApiErrorKind Kind { get; }

    public int? StatusCode { get; }

    public string Code { get; }

    public JsonElement? Details { get; }

    public bool IsConnectionProblem => Kind is ApiErrorKind.Network or ApiErrorKind.Timeout;

    public static ApiException Network(Exception inner) =>
        new(ApiErrorKind.Network, null, ApiErrorCodes.Network, "The server could not be reached.", null, inner);

    public static ApiException Timeout(Exception inner) =>
        new(ApiErrorKind.Timeout, null, ApiErrorCodes.Timeout, "The server did not respond in time.", null, inner);

    public static ApiException InvalidResponse(int? status, Exception? inner = null) =>
        new(ApiErrorKind.InvalidResponse, status, ApiErrorCodes.InvalidResponse, "The server response was not understood.", null, inner);

    public static ApiException SessionExpired() =>
        new(ApiErrorKind.Http, 401, ApiErrorCodes.Unauthorized, "The session has expired.");
}
