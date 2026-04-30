using System.Net;

namespace Odin.Api.Services.Paddle;

/// <summary>
/// Thrown when the Paddle API returns a non-success response. Wraps the Paddle error envelope
/// (<c>error.type</c>, <c>error.code</c>, <c>error.detail</c>) plus the HTTP status and Paddle request id.
/// </summary>
public sealed class PaddleApiException : Exception
{
    public HttpStatusCode StatusCode { get; }
    public string? PaddleRequestId { get; }
    public string? PaddleErrorType { get; }
    public string? PaddleErrorCode { get; }
    public string? RawBody { get; }
    public IReadOnlyList<PaddleValidationError> ValidationErrors { get; }

    public PaddleApiException(
        HttpStatusCode statusCode,
        string message,
        string? paddleRequestId,
        string? paddleErrorType,
        string? paddleErrorCode,
        string? rawBody,
        IReadOnlyList<PaddleValidationError>? validationErrors = null,
        Exception? inner = null)
        : base(message, inner)
    {
        StatusCode = statusCode;
        PaddleRequestId = paddleRequestId;
        PaddleErrorType = paddleErrorType;
        PaddleErrorCode = paddleErrorCode;
        RawBody = rawBody;
        ValidationErrors = validationErrors ?? Array.Empty<PaddleValidationError>();
    }

    public bool IsTransient =>
        (int)StatusCode >= 500 || StatusCode == HttpStatusCode.TooManyRequests;
}

public sealed record PaddleValidationError(string Field, string Message);
