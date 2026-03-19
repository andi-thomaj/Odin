namespace Odin.Api.Middleware
{
    /// <summary>
    /// Middleware that adds security headers to all HTTP responses
    /// to protect against common web vulnerabilities.
    /// </summary>
    public class SecurityHeadersMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly IWebHostEnvironment _environment;

        public SecurityHeadersMiddleware(RequestDelegate next, IWebHostEnvironment environment)
        {
            _next = next;
            _environment = environment;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var response = context.Response;

            // Prevent clickjacking
            response.Headers["X-Frame-Options"] = "DENY";

            // Prevent MIME type sniffing
            response.Headers["X-Content-Type-Options"] = "nosniff";

            // XSS protection for legacy browsers
            response.Headers["X-XSS-Protection"] = "1; mode=block";

            // Referrer policy
            response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";

            // Permissions policy - restrict access to browser features
            response.Headers["Permissions-Policy"] = 
                "camera=(), microphone=(), geolocation=(), interest-cohort=()";

            // HSTS - only in production
            if (!_environment.IsDevelopment())
            {
                response.Headers["Strict-Transport-Security"] = 
                    "max-age=31536000; includeSubDomains";
            }

            await _next(context);
        }
    }

    public static class SecurityHeadersMiddlewareExtensions
    {
        public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<SecurityHeadersMiddleware>();
        }
    }
}
