using Odin.Api.Authentication;
using Odin.Api.Endpoints.AncestralPortraitManagement.Models;
using Odin.Api.Endpoints.Payments.Models;

namespace Odin.Api.Endpoints.AncestralPortraitManagement;

/// <summary>
/// The paid "Through the Ages" AI ancestral-portraits add-on (<c>/v1/api/orders/{orderId}/ancestral-portraits</c> +
/// <c>/v1/api/ancestral-portraits/{id}</c>). Unlock via a StoreKit add-on purchase, generate per-era portraits from
/// the user's face photos (gpt-image-2 edits), poll the set, pick a variation per era, and stream a portrait's PRIVATE
/// bytes (authenticated only — never a public URL, these are images of the user's face).
/// </summary>
public static class AncestralPortraitEndpoints
{
    public static void MapAncestralPortraitEndpoints(this IEndpointRouteBuilder app)
    {
        var perOrder = app.MapGroup("api/orders/{orderId:int}/ancestral-portraits").RequireAuthorization("EmailVerified");

        perOrder.MapPost("/purchase", Purchase)
            .RequireRateLimiting("strict")
            .Produces<AncestralPortraitSetContract.Response>(StatusCodes.Status201Created)
            .Produces<AncestralPortraitSetContract.Response>(StatusCodes.Status200OK);

        perOrder.MapGet("/", GetSet)
            .RequireRateLimiting("authenticated")
            .Produces<AncestralPortraitSetContract.Response>(StatusCodes.Status200OK);

        perOrder.MapPost("/generate", Generate)
            .RequireRateLimiting("strict")
            .Produces(StatusCodes.Status202Accepted);

        var perPortrait = app.MapGroup("api/ancestral-portraits").RequireAuthorization("EmailVerified");

        perPortrait.MapPost("/{portraitId:int}/select", Select)
            .RequireRateLimiting("authenticated")
            .Produces(StatusCodes.Status200OK);

        perPortrait.MapGet("/{portraitId:int}/download", Download)
            .RequireRateLimiting("authenticated");
    }

    private static async Task<IResult> Purchase(
        IAncestralPortraitService service, HttpContext httpContext, int orderId,
        PurchaseAncestralPortraitsContract.Request request, CancellationToken cancellationToken)
    {
        var identityId = httpContext.User.GetIdentityId() ?? string.Empty;
        try
        {
            var (response, statusCode, error) = await service.PurchaseAsync(orderId, identityId, request.AppStoreTransaction, cancellationToken);
            return statusCode switch
            {
                201 => Results.Created($"/v1/api/orders/{orderId}/ancestral-portraits", response),
                200 => Results.Ok(response),
                403 => Results.Forbid(),
                _ => Results.NotFound(new { Message = error }),
            };
        }
        catch (AppStorePurchaseException ex)
        {
            return Results.BadRequest(new { Message = ex.Message });
        }
    }

    private static async Task<IResult> GetSet(
        IAncestralPortraitService service, HttpContext httpContext, int orderId, CancellationToken cancellationToken)
    {
        var identityId = httpContext.User.GetIdentityId() ?? string.Empty;
        var (response, statusCode) = await service.GetSetAsync(orderId, identityId, cancellationToken);
        return statusCode switch
        {
            200 => Results.Ok(response),
            403 => Results.Forbid(),
            _ => Results.NotFound(),
        };
    }

    private static async Task<IResult> Generate(
        IAncestralPortraitService service, HttpContext httpContext, int orderId, CancellationToken cancellationToken)
    {
        var identityId = httpContext.User.GetIdentityId() ?? string.Empty;
        var statusCode = await service.RequestGenerateAsync(orderId, identityId, cancellationToken);
        return statusCode switch
        {
            202 => Results.Accepted(),
            403 => Results.Forbid(),
            _ => Results.NotFound(),
        };
    }

    private static async Task<IResult> Select(
        IAncestralPortraitService service, HttpContext httpContext, int portraitId, CancellationToken cancellationToken)
    {
        var identityId = httpContext.User.GetIdentityId() ?? string.Empty;
        var statusCode = await service.SelectAsync(portraitId, identityId, cancellationToken);
        return statusCode switch
        {
            200 => Results.Ok(),
            403 => Results.Forbid(),
            _ => Results.NotFound(),
        };
    }

    private static async Task<IResult> Download(
        IAncestralPortraitService service, HttpContext httpContext, int portraitId, CancellationToken cancellationToken)
    {
        var identityId = httpContext.User.GetIdentityId() ?? string.Empty;
        var (bytes, contentType, statusCode) = await service.GetPortraitBytesAsync(portraitId, identityId, cancellationToken);
        return statusCode switch
        {
            200 => Results.File(bytes!, contentType ?? "image/jpeg"),
            403 => Results.Forbid(),
            _ => Results.NotFound(),
        };
    }
}
