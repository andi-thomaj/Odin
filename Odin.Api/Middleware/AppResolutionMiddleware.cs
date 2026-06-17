using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Odin.Api.Authentication;
using Odin.Api.Services;

namespace Odin.Api.Middleware
{
    /// <summary>
    /// Resolves the calling application from the <c>X-App</c> request header and records it on the scoped
    /// <see cref="RequestAppContext"/> (and <c>HttpContext.Items["App"]</c>) for the rest of the request. The
    /// auth hot path (<see cref="Services.IUserProvisioningService"/>, <see cref="RoleEnrichmentMiddleware"/>)
    /// and <see cref="Data.ApplicationDbContext"/>'s app-scoped query filters + write stamping all read it.
    ///
    /// <para>Missing/blank header → <see cref="AppKeys.Ancestrify"/> (the original app) so legacy callers and
    /// server-internal traffic keep working. A header naming an unknown or inactive app → <c>400</c>, since it
    /// signals a misconfigured or typo'd client. Must run AFTER authentication and BEFORE role enrichment
    /// (provisioning/role lookup now key on the resolved app).</para>
    /// </summary>
    public class AppResolutionMiddleware(RequestDelegate next)
    {
        public const string HeaderName = "X-App";
        public const string QueryParamName = "app";
        public const string HttpContextItemKey = "App";

        public async Task InvokeAsync(
            HttpContext context,
            RequestAppContext appContext,
            IApplicationRegistry registry)
        {
            // Header is the primary signal for normal API calls. SignalR's WebSocket/negotiate requests can't
            // set a custom header, so the hub URL carries the app as a query-string param — accepted here as a
            // fallback so those requests resolve the right app (and don't mis-provision under the default).
            var header = context.Request.Headers[HeaderName].FirstOrDefault();
            var key = (string.IsNullOrWhiteSpace(header)
                ? context.Request.Query[QueryParamName].FirstOrDefault()
                : header)?.Trim();

            string resolved;
            if (string.IsNullOrEmpty(key))
            {
                resolved = AppKeys.Ancestrify;
            }
            else
            {
                var app = await registry.GetActiveAsync(key, context.RequestAborted);
                if (app is null)
                {
                    context.Response.StatusCode = StatusCodes.Status400BadRequest;
                    context.Response.ContentType = "application/problem+json";
                    await context.Response.WriteAsync(
                        "{\"title\":\"Unknown application\",\"status\":400," +
                        "\"detail\":\"The X-App header names an unknown or inactive application.\"}",
                        context.RequestAborted);
                    return;
                }

                resolved = app.Key; // canonical casing from the registry
            }

            appContext.SetApp(resolved);
            context.Items[HttpContextItemKey] = resolved;

            await next(context);
        }
    }

    public static class AppResolutionMiddlewareExtensions
    {
        public static IApplicationBuilder UseAppResolution(this IApplicationBuilder builder)
            => builder.UseMiddleware<AppResolutionMiddleware>();
    }
}
