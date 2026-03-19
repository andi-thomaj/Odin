namespace Odin.Api.Middleware
{
    /// <summary>
    /// Middleware that generates a unique request ID for each request
    /// and includes it in the response headers and context for logging correlation.
    /// </summary>
    public class RequestIdMiddleware
    {
        private readonly RequestDelegate _next;
        private const string RequestIdHeaderName = "X-Request-ID";
        private const string RequestIdContextKey = "RequestId";

        public RequestIdMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            // Use existing request ID from header if present, otherwise generate new one
            var requestId = context.Request.Headers[RequestIdHeaderName].FirstOrDefault()
                           ?? Guid.NewGuid().ToString("N");

            // Store in context for use by other middleware and logging
            context.Items[RequestIdContextKey] = requestId;

            // Add to response headers
            context.Response.Headers[RequestIdHeaderName] = requestId;

            await _next(context);
        }
    }

    public static class RequestIdMiddlewareExtensions
    {
        public static IApplicationBuilder UseRequestId(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<RequestIdMiddleware>();
        }
    }
}
