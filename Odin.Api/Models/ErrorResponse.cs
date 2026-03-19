namespace Odin.Api.Models
{
    /// <summary>
    /// Standard error response model for consistent API error handling.
    /// </summary>
    public class ErrorResponse
    {
        public string RequestId { get; set; } = string.Empty;
        public int StatusCode { get; set; }
        public string Message { get; set; } = string.Empty;
        public string ErrorCode { get; set; } = string.Empty;
        public string? Details { get; set; }
    }
}
