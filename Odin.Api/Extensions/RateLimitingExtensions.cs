using System.Net;
using System.Text.Json;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Odin.Api.Models;

namespace Odin.Api.Extensions
{
    /// <summary>
    /// Rate-limiting policy registration, extracted from Program.cs to keep startup readable. Policies:
    /// a global per-user/IP fixed window (100/min authenticated, 30/min anonymous), plus named
    /// "authenticated" (sliding), "file-upload"/"strict" (fixed) and "concurrent" policies.
    /// NOTE: <c>app.UseRateLimiter()</c> must run AFTER <c>UseAuthentication()</c> so the authenticated
    /// tier and per-user partitioning see a populated <c>context.User</c> (see Program.cs pipeline).
    /// </summary>
    public static class RateLimitingExtensions
    {
        public static IServiceCollection AddOdinRateLimiting(this IServiceCollection services)
        {
            services.AddRateLimiter(options =>
            {
                options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
                options.OnRejected = async (context, cancellationToken) =>
                {
                    // Extract RetryAfter from lease metadata if available
                    if (context.Lease.TryGetMetadata(MetadataName.RetryAfter.Name, out var retryAfterObj)
                        && retryAfterObj is TimeSpan retryAfter)
                    {
                        context.HttpContext.Response.Headers["Retry-After"] =
                            ((int)retryAfter.TotalSeconds).ToString();
                    }

                    // Extract policy name from endpoint metadata
                    var policyName = context.HttpContext.GetEndpoint()
                        ?.Metadata
                        .GetMetadata<EnableRateLimitingAttribute>()
                        ?.PolicyName ?? "unknown";

                    var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
                    var requestId = context.HttpContext.Items.TryGetValue("RequestId", out var id)
                        ? id?.ToString()
                        : "unknown";

                    logger.LogWarning(
                        "Rate limit exceeded. RequestId: {RequestId}, Policy: {Policy}, Path: {Path}",
                        requestId,
                        policyName,
                        context.HttpContext.Request.Path);

                    context.HttpContext.Response.ContentType = "application/json";
                    var errorBody = new ErrorResponse
                    {
                        RequestId = requestId ?? string.Empty,
                        StatusCode = StatusCodes.Status429TooManyRequests,
                        Message = "Rate limit exceeded. Please try again later.",
                        ErrorCode = "RATE_LIMIT_EXCEEDED"
                    };
                    var jsonOptions = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
                    await context.HttpContext.Response.WriteAsync(
                        JsonSerializer.Serialize(errorBody, jsonOptions), cancellationToken);
                };

                // Helper to get client IP (handles X-Forwarded-For)
                static string GetClientIp(HttpContext context)
                {
                    var forwardedFor = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
                    if (!string.IsNullOrEmpty(forwardedFor))
                    {
                        // Take first IP from comma-separated list
                        var ip = forwardedFor.Split(',')[0].Trim();
                        if (IPAddress.TryParse(ip, out _))
                            return ip;
                    }
                    return context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
                }

                // Helper to get partition key (user ID or IP)
                static string GetPartitionKey(HttpContext context)
                {
                    return context.User.Identity?.IsAuthenticated == true
                        ? context.User.Identity.Name ?? GetClientIp(context)
                        : GetClientIp(context);
                }

                // Global default policy - 30 req/min for unauthenticated, 100 req/min for authenticated
                // Exclude SignalR endpoints from global rate limiting (they have their own connection management)
                options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
                {
                    // Skip rate limiting for SignalR hubs (negotiation and connection endpoints) and for
                    // the Hangfire dashboard under /jobs — its stats endpoint auto-polls every couple of
                    // seconds, which trips the per-minute window and surfaces as a 429 in the dashboard.
                    // /jobs is already restricted to Admins by HangfireDashboardAuthFilter, so it doesn't
                    // need IP/user rate limiting.
                    var path = context.Request.Path.Value ?? "";
                    if (path.StartsWith("/hubs/", StringComparison.OrdinalIgnoreCase)
                        || path.StartsWith("/jobs", StringComparison.OrdinalIgnoreCase))
                    {
                        // Return a no-op limiter for these endpoints
                        return RateLimitPartition.GetNoLimiter(partitionKey: "");
                    }

                    return RateLimitPartition.GetFixedWindowLimiter(
                        partitionKey: GetPartitionKey(context),
                        factory: _ =>
                        {
                            var isAuthenticated = context.User.Identity?.IsAuthenticated == true;
                            return new FixedWindowRateLimiterOptions
                            {
                                PermitLimit = isAuthenticated ? 100 : 30,
                                Window = TimeSpan.FromMinutes(1),
                                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                                QueueLimit = 0
                            };
                        });
                });

                // Authenticated policy - sliding window for better distribution
                options.AddPolicy("authenticated", httpContext =>
                    RateLimitPartition.GetSlidingWindowLimiter(
                        partitionKey: GetPartitionKey(httpContext),
                        factory: _ => new SlidingWindowRateLimiterOptions
                        {
                            PermitLimit = 100,
                            Window = TimeSpan.FromMinutes(1),
                            SegmentsPerWindow = 4,
                            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                            QueueLimit = 0
                        }));

                // File upload policy - strict limits
                options.AddPolicy("file-upload", httpContext =>
                    RateLimitPartition.GetFixedWindowLimiter(
                        partitionKey: GetPartitionKey(httpContext),
                        factory: _ => new FixedWindowRateLimiterOptions
                        {
                            PermitLimit = 10,
                            Window = TimeSpan.FromMinutes(1),
                            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                            QueueLimit = 0
                        }));

                // Strict policy for sensitive/admin endpoints
                options.AddPolicy("strict", httpContext =>
                    RateLimitPartition.GetFixedWindowLimiter(
                        partitionKey: GetPartitionKey(httpContext),
                        factory: _ => new FixedWindowRateLimiterOptions
                        {
                            PermitLimit = 20,
                            Window = TimeSpan.FromMinutes(1),
                            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                            QueueLimit = 0
                        }));

                // Concurrency limiter for resource-intensive operations
                options.AddPolicy("concurrent", httpContext =>
                    RateLimitPartition.GetConcurrencyLimiter(
                        partitionKey: GetPartitionKey(httpContext),
                        factory: _ => new ConcurrencyLimiterOptions
                        {
                            PermitLimit = 5,
                            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                            QueueLimit = 0
                        }));
            });

            return services;
        }
    }
}
