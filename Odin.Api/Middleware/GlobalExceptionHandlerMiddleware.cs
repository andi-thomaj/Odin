using System.Net;
using System.Text.Json;
using Odin.Api.Models;

namespace Odin.Api.Middleware
{
    /// <summary>
    /// Global exception handler middleware that catches all unhandled exceptions,
    /// sanitizes error messages, and returns consistent error responses.
    /// </summary>
    public class GlobalExceptionHandlerMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<GlobalExceptionHandlerMiddleware> _logger;
        private readonly IWebHostEnvironment _environment;

        public GlobalExceptionHandlerMiddleware(
            RequestDelegate next,
            ILogger<GlobalExceptionHandlerMiddleware> logger,
            IWebHostEnvironment environment)
        {
            _next = next;
            _logger = logger;
            _environment = environment;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (Exception ex)
            {
                await HandleExceptionAsync(context, ex);
            }
        }

        private async Task HandleExceptionAsync(HttpContext context, Exception exception)
        {
            var requestId = context.Items.TryGetValue("RequestId", out var id) && id is not null
                ? id.ToString() ?? Guid.NewGuid().ToString()
                : Guid.NewGuid().ToString();

            _logger.LogError(
                exception,
                "Unhandled exception occurred. RequestId: {RequestId}, Path: {Path}, Method: {Method}",
                requestId,
                context.Request.Path,
                context.Request.Method);

            var (statusCode, message, errorCode) = MapExceptionToResponse(exception);

            var errorResponse = new ErrorResponse
            {
                RequestId = requestId,
                StatusCode = statusCode,
                Message = message,
                ErrorCode = errorCode
            };

            // Include stack trace only in development
            if (_environment.IsDevelopment())
            {
                errorResponse.Details = exception.ToString();
            }

            context.Response.StatusCode = statusCode;
            context.Response.ContentType = "application/json";

            var options = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };

            await context.Response.WriteAsync(JsonSerializer.Serialize(errorResponse, options));
        }

        private static (int StatusCode, string Message, string ErrorCode) MapExceptionToResponse(Exception exception)
        {
            return exception switch
            {
                InvalidOperationException => (StatusCodes.Status400BadRequest, 
                    "The request is invalid.", "INVALID_OPERATION"),
                
                UnauthorizedAccessException => (StatusCodes.Status401Unauthorized, 
                    "You are not authorized to perform this action.", "UNAUTHORIZED"),
                
                KeyNotFoundException => (StatusCodes.Status404NotFound, 
                    "The requested resource was not found.", "NOT_FOUND"),
                
                ArgumentException or ArgumentNullException => (StatusCodes.Status400BadRequest, 
                    "Invalid arguments provided.", "INVALID_ARGUMENT"),
                
                TimeoutException => (StatusCodes.Status408RequestTimeout, 
                    "The request timed out.", "TIMEOUT"),
                
                NotSupportedException => (StatusCodes.Status501NotImplemented, 
                    "This operation is not supported.", "NOT_SUPPORTED"),
                
                // Database exceptions - sanitize
                Microsoft.EntityFrameworkCore.DbUpdateException => (StatusCodes.Status500InternalServerError, 
                    "A database error occurred. Please try again later.", "DATABASE_ERROR"),
                
                Npgsql.PostgresException => (StatusCodes.Status500InternalServerError, 
                    "A database error occurred. Please try again later.", "DATABASE_ERROR"),
                
                // Default to 500
                _ => (StatusCodes.Status500InternalServerError, 
                    "An unexpected error occurred. Please try again later.", "INTERNAL_ERROR")
            };
        }
    }

    public static class GlobalExceptionHandlerMiddlewareExtensions
    {
        public static IApplicationBuilder UseGlobalExceptionHandler(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<GlobalExceptionHandlerMiddleware>();
        }
    }
}
