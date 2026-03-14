using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
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
        public static void MapOrderEndpoints(this IEndpointRouteBuilder app)
        {
            var endpoints = app.MapGroup("api/orders");

            endpoints.MapGet("/", GetAll).RequireAuthorization("Authenticated");
            endpoints.MapGet("/{id:int}", GetById).RequireAuthorization("Authenticated");
            endpoints.MapPost("/", Create).DisableAntiforgery().RequireAuthorization("Authenticated");
            endpoints.MapPut("/{id:int}", Update).DisableAntiforgery().RequireAuthorization("Authenticated");
            endpoints.MapDelete("/{id:int}", Delete).RequireAuthorization("AdminOnly");
            endpoints.MapGet("/{id:int}/qpadm-result", GetQpadmResult).RequireAuthorization("Authenticated");
            endpoints.MapGet("/{id:int}/merged-data/download", DownloadMergedData).RequireAuthorization("Authenticated");
            endpoints.MapGet("/{id:int}/profile-picture", GetProfilePicture).RequireAuthorization("Authenticated");
            endpoints.MapPost("/{id:int}/mark-viewed", MarkResultsAsViewed).RequireAuthorization("Authenticated");
        }

        private static async Task<IResult> GetAll(IOrderService service)
        {
            var orders = await service.GetAllAsync();
            return Results.Ok(orders);
        }

        private static async Task<IResult> GetById(IOrderService service, int id)
        {
            var order = await service.GetByIdAsync(id);

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
            int id,
            [FromForm] UpdateOrderContract.Request request)
        {
            var validationProblem = request.ValidateAndGetProblem();
            if (validationProblem is not null)
            {
                return validationProblem;
            }

            try
            {
                var response = await service.UpdateAsync(id, request);

                return response is null
                    ? Results.NotFound(new { Message = $"Order with ID {id} not found." })
                    : Results.Ok(response);
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

            var (result, statusCode, error) = await service.GetQpadmResultForOrderAsync(id, identityId);

            return statusCode switch
            {
                200 => Results.Ok(result),
                403 => Results.Forbid(),
                400 => Results.BadRequest(new { Message = error }),
                _ => Results.NotFound(new { Message = error })
            };
        }

        private static async Task<IResult> DownloadMergedData(IOrderService service, HttpContext httpContext, int id)
        {
            var identityId = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier)
                             ?? httpContext.User.FindFirstValue("sub")
                             ?? string.Empty;

            var (fileBytes, fileName, statusCode, error) = await service.DownloadMergedDataForOrderAsync(id, identityId);

            return statusCode switch
            {
                200 => Results.File(fileBytes!, "application/octet-stream", fileName),
                403 => Results.Forbid(),
                400 => Results.BadRequest(new { Message = error }),
                _ => Results.NotFound(new { Message = error })
            };
        }

        private static async Task<IResult> GetProfilePicture(IOrderService service, int id)
        {
            var result = await service.GetProfilePictureAsync(id);
            if (result is null)
                return Results.NotFound(new { Message = $"No profile picture found for order with ID {id}." });

            var (fileBytes, fileName) = result.Value;
            var extension = Path.GetExtension(fileName ?? "").ToLowerInvariant();
            var contentType = ImageContentTypes.GetValueOrDefault(extension, "application/octet-stream");

            return Results.File(fileBytes!, contentType, fileName);
        }

        private static async Task<IResult> MarkResultsAsViewed(IOrderService service, HttpContext httpContext, int id)
        {
            var identityId = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier)
                             ?? httpContext.User.FindFirstValue("sub")
                             ?? string.Empty;

            var (success, statusCode, error) = await service.MarkResultsAsViewedAsync(id, identityId);

            return statusCode switch
            {
                200 => Results.Ok(new { Success = success }),
                403 => Results.Forbid(),
                _ => Results.NotFound(new { Message = error })
            };
        }

    }
}
