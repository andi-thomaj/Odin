using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Odin.Api.Data.Entities;
using Odin.Api.Data.Enums;
using Odin.Api.Endpoints.MergeManagement;
using Odin.Api.Endpoints.OrderManagement.Models;
using Odin.Api.Extensions;

namespace Odin.Api.Endpoints.OrderManagement
{
    public static class OrderEndpoints
    {
        private static readonly Dictionary<string, string> ImageContentTypes = new(StringComparer.OrdinalIgnoreCase)
        {
            [".jpg"] = "image/jpeg",
            [".jpeg"] = "image/jpeg",
            [".png"] = "image/png",
            [".webp"] = "image/webp"
        };

        private static bool IsAdmin(HttpContext httpContext) =>
            httpContext.User.HasClaim("app_role", AppRole.Admin.ToString());

        public static void MapOrderEndpoints(this IEndpointRouteBuilder app)
        {
            var endpoints = app.MapGroup("api/orders");

            endpoints.MapGet("/", GetAll)
                .RequireAuthorization("EmailVerified")
                .RequireRateLimiting("authenticated")
                .Produces<IEnumerable<GetOrderContract.Response>>(StatusCodes.Status200OK);

            var adminEndpoints = app.MapGroup("api/admin/orders")
                .RequireAuthorization("AdminOnly");

            adminEndpoints.MapGet("/", GetAllAdmin)
                .RequireRateLimiting("authenticated")
                .Produces<IEnumerable<AdminGetOrderContract.Response>>(StatusCodes.Status200OK);

            endpoints.MapGet("/{id:int}", GetById)
                .RequireAuthorization("EmailVerified")
                .RequireRateLimiting("authenticated")
                .Produces<GetOrderContract.Response>(StatusCodes.Status200OK);

            endpoints.MapPost("/", Create)
                .DisableAntiforgery()
                .RequireAuthorization("EmailVerified")
                .RequireRateLimiting("file-upload")
                .Produces<CreateOrderContract.Response>(StatusCodes.Status201Created)
                .WithRequestTimeout(TimeSpan.FromMinutes(5));

            endpoints.MapPut("/{id:int}", Update)
                .DisableAntiforgery()
                .RequireAuthorization("EmailVerified")
                .RequireRateLimiting("file-upload")
                .Produces<GetOrderContract.Response>(StatusCodes.Status200OK)
                .WithRequestTimeout(TimeSpan.FromMinutes(5));

            endpoints.MapDelete("/{id:int}", Delete)
                .RequireAuthorization("AdminOnly")
                .RequireRateLimiting("strict")
                .Produces(StatusCodes.Status204NoContent);

            endpoints.MapGet("/{id:int}/qpadm-result", GetQpadmResult)
                .RequireAuthorization("EmailVerified")
                .RequireRateLimiting("authenticated")
                .Produces<GetOrderQpadmResultContract.Response>(StatusCodes.Status200OK);

            endpoints.MapGet("/{id:int}/g25-result", GetG25Result)
                .RequireAuthorization("EmailVerified")
                .RequireRateLimiting("authenticated")
                .Produces<GetOrderG25ResultContract.Response>(StatusCodes.Status200OK);

            endpoints.MapGet("/{id:int}/merged-data/download", DownloadMergedData)
                .RequireAuthorization("EmailVerified")
                .RequireRateLimiting("authenticated");

            endpoints.MapGet("/{id:int}/profile-picture", GetProfilePicture)
                .RequireAuthorization("EmailVerified")
                .RequireRateLimiting("authenticated");

            app.MapPatch("api/qpadm-orders/{id:int}/viewed-status", MarkQpadmResultsAsViewed)
                .RequireAuthorization("EmailVerified")
                .RequireRateLimiting("authenticated");

            app.MapPatch("api/g25-orders/{id:int}/viewed-status", MarkG25ResultsAsViewed)
                .RequireAuthorization("EmailVerified")
                .RequireRateLimiting("authenticated");
        }

        private static async Task<IResult> GetAll(IOrderService service, HttpContext httpContext)
        {
            var identityId = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier)
                             ?? httpContext.User.FindFirstValue("sub")
                             ?? string.Empty;

            var orders = await service.GetAllAsync(identityId);
            return Results.Ok(orders);
        }

        private static async Task<IResult> GetAllAdmin(IOrderService service, int? skip = null, int? take = null)
        {
            // skip/take are optional query params; omitting them returns all orders (unchanged behaviour).
            var orders = await service.GetAllAdminAsync(skip, take);
            return Results.Ok(orders);
        }

        private static async Task<IResult> GetById(IOrderService service, HttpContext httpContext, int id)
        {
            var identityId = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier)
                             ?? httpContext.User.FindFirstValue("sub")
                             ?? string.Empty;

            var order = await service.GetByIdAsync(id, identityId);

            return order is null
                ? Results.NotFound(new { Message = $"Order with ID {id} not found." })
                : Results.Ok(order);
        }

        private static async Task<IResult> Create(
            IOrderService service,
            HttpContext httpContext,
            [FromForm] CreateOrderContract.Request request)
        {
            var validationProblem = request.ValidateAndGetProblem();
            if (validationProblem is not null)
            {
                return validationProblem;
            }

            var identityId = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier)
                             ?? httpContext.User.FindFirstValue("sub")
                             ?? string.Empty;

            var ipAddress = httpContext.Request.Headers["X-Forwarded-For"]
                                .FirstOrDefault()?.Split(',')[0].Trim()
                            ?? httpContext.Connection.RemoteIpAddress?.ToString();

            try
            {
                var response = await service.CreateAsync(request, identityId, ipAddress);
                return Results.Created($"/api/orders/{response.Id}", response);
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { Message = ex.Message });
            }
        }

        private static async Task<IResult> Update(
            IOrderService service,
            HttpContext httpContext,
            int id,
            [FromForm] UpdateOrderContract.Request request)
        {
            var validationProblem = request.ValidateAndGetProblem();
            if (validationProblem is not null)
            {
                return validationProblem;
            }

            var identityId = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier)
                             ?? httpContext.User.FindFirstValue("sub")
                             ?? string.Empty;

            try
            {
                var (response, statusCode) = await service.UpdateAsync(id, identityId, request);

                return statusCode switch
                {
                    200 => Results.Ok(response),
                    403 => Results.Forbid(),
                    _ => Results.NotFound(new { Message = $"Order with ID {id} not found." })
                };
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { Message = ex.Message });
            }
        }

        private static async Task<IResult> Delete(IOrderService service, int id)
        {
            var deleted = await service.DeleteAsync(id);

            return deleted
                ? Results.NoContent()
                : Results.NotFound(new { Message = $"Order with ID {id} not found." });
        }

        private static async Task<IResult> GetQpadmResult(IOrderService service, HttpContext httpContext, int id)
        {
            var identityId = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier)
                             ?? httpContext.User.FindFirstValue("sub")
                             ?? string.Empty;

            var (result, statusCode, error) = await service.GetQpadmResultForOrderAsync(id, identityId, IsAdmin(httpContext));

            return statusCode switch
            {
                200 => Results.Ok(result),
                403 => Results.Forbid(),
                400 => Results.BadRequest(new { Message = error }),
                _ => Results.NotFound(new { Message = error })
            };
        }

        private static async Task<IResult> GetG25Result(IOrderService service, HttpContext httpContext, int id)
        {
            var identityId = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier)
                             ?? httpContext.User.FindFirstValue("sub")
                             ?? string.Empty;

            var (result, statusCode, error) = await service.GetG25ResultForOrderAsync(id, identityId, IsAdmin(httpContext));

            return statusCode switch
            {
                200 => Results.Ok(result),
                403 => Results.Forbid(),
                400 => Results.BadRequest(new { Message = error }),
                _ => Results.NotFound(new { Message = error })
            };
        }

        private static async Task<IResult> DownloadMergedData(
            IOrderService service, IMergePipelineService mergeService, HttpContext httpContext, int id)
        {
            var identityId = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier)
                             ?? httpContext.User.FindFirstValue("sub")
                             ?? string.Empty;

            var (statusCode, error, mergeId, fileName, legacyBytes) =
                await service.ResolveMergedDataDownloadAsync(id, identityId, IsAdmin(httpContext));

            if (statusCode != 200)
            {
                return statusCode switch
                {
                    403 => Results.Forbid(),
                    400 => Results.BadRequest(new { Message = error }),
                    _ => Results.NotFound(new { Message = error })
                };
            }

            // Legacy hand-uploaded blob: serve inline.
            if (legacyBytes is not null)
                return Results.File(legacyBytes, "application/octet-stream", fileName);

            // Automated merge bundle: stream it straight from the tools-api so the multi-GB body never
            // buffers in this process. The upstream response is disposed once the body is copied.
            var ct = httpContext.RequestAborted;
            HttpResponseMessage upstream;
            try
            {
                upstream = await mergeService.OpenDownloadAsync(mergeId!, ct);
            }
            catch (MergePipelineException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return Results.NotFound(new { Message = "The merged dataset is no longer available for this order." });
            }

            return Results.Stream(async outputStream =>
            {
                using (upstream)
                await using (var body = await upstream.Content.ReadAsStreamAsync(ct))
                {
                    await body.CopyToAsync(outputStream, ct);
                }
            }, "application/gzip", fileName);
        }

        // serviceType disambiguates the qpAdm vs G25 order tables (which share an ID space); defaults to
        // qpAdm so existing callers that omit it keep working.
        private static async Task<IResult> GetProfilePicture(IOrderService orderService, HttpContext httpContext, int id, [FromQuery(Name = "service")] ServiceType serviceType = ServiceType.qpAdm)
        {
            var identityId = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier)
                             ?? httpContext.User.FindFirstValue("sub")
                             ?? string.Empty;

            var (fileBytes, fileName, statusCode, error) = await orderService.GetProfilePictureAsync(id, identityId, serviceType, IsAdmin(httpContext));

            return statusCode switch
            {
                200 => Results.File(fileBytes!, ImageContentTypes.GetValueOrDefault(Path.GetExtension(fileName ?? "").ToLowerInvariant(), "application/octet-stream"), fileName),
                403 => Results.Forbid(),
                _ => Results.NotFound(new { Message = error })
            };
        }

        private static async Task<IResult> MarkQpadmResultsAsViewed(IOrderService service, HttpContext httpContext, int id)
        {
            var identityId = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier)
                             ?? httpContext.User.FindFirstValue("sub")
                             ?? string.Empty;

            var (success, statusCode, error) = await service.MarkQpadmResultsAsViewedAsync(id, identityId, IsAdmin(httpContext));

            return statusCode switch
            {
                200 => Results.Ok(new { Success = success }),
                403 => Results.Forbid(),
                _ => Results.NotFound(new { Message = error })
            };
        }

        private static async Task<IResult> MarkG25ResultsAsViewed(IOrderService service, HttpContext httpContext, int id)
        {
            var identityId = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier)
                             ?? httpContext.User.FindFirstValue("sub")
                             ?? string.Empty;

            var (success, statusCode, error) = await service.MarkG25ResultsAsViewedAsync(id, identityId, IsAdmin(httpContext));

            return statusCode switch
            {
                200 => Results.Ok(new { Success = success }),
                403 => Results.Forbid(),
                _ => Results.NotFound(new { Message = error })
            };
        }

    }
}
