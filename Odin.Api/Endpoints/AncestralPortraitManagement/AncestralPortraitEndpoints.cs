using Odin.Api.Authentication;
using Odin.Api.Endpoints.AncestralPortraitManagement.Models;
using Odin.Api.Endpoints.Payments.Models;
using Odin.Api.Extensions;

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

        // All iterations for this order, newest first (each re-purchase is a kept iteration).
        perOrder.MapGet("/", ListSets)
            .RequireRateLimiting("authenticated")
            .Produces<List<AncestralPortraitSetContract.Response>>(StatusCodes.Status200OK);

        var perSet = app.MapGroup("api/ancestral-portraits/sets/{setId:guid}").RequireAuthorization("EmailVerified");

        // Poll one iteration while it generates.
        perSet.MapGet("/", GetSetById)
            .RequireRateLimiting("authenticated")
            .Produces<AncestralPortraitSetContract.Response>(StatusCodes.Status200OK);

        // (Re)run generation for one iteration (retry a failed run / after capturing a face set).
        perSet.MapPost("/generate", Generate)
            .RequireRateLimiting("strict")
            .Produces(StatusCodes.Status202Accepted);

        // Delete one iteration entirely — removes its private portrait objects from R2 + the rows.
        perSet.MapDelete("/", DeleteSet)
            .RequireRateLimiting("strict")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status409Conflict);

        var perPortrait = app.MapGroup("api/ancestral-portraits").RequireAuthorization("EmailVerified");

        perPortrait.MapPost("/{portraitId:int}/select", Select)
            .RequireRateLimiting("authenticated")
            .Produces(StatusCodes.Status200OK);

        perPortrait.MapGet("/{portraitId:int}/download", Download)
            .RequireRateLimiting("authenticated");

        // Admin cost/usage dashboard — total AI-portrait spend across all runs (first-party, real-time).
        app.MapGet("api/admin/ancestral-portraits/usage", Usage)
            .RequireAuthorization("AdminOnly")
            .RequireRateLimiting("authenticated")
            .Produces<AncestralPortraitUsageContract.Response>(StatusCodes.Status200OK);

        // Admin settings — fully runtime-configurable generation (model, quality, size, variations, caps, cost rates).
        app.MapGet("api/admin/ancestral-portraits/settings", GetSettings)
            .RequireAuthorization("AdminOnly")
            .RequireRateLimiting("authenticated")
            .Produces<AncestralPortraitSettingsContract.Response>(StatusCodes.Status200OK);

        app.MapPut("api/admin/ancestral-portraits/settings", UpdateSettings)
            .RequireAuthorization("AdminOnly")
            .RequireRateLimiting("strict")
            .Produces<AncestralPortraitSettingsContract.Response>(StatusCodes.Status200OK);
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

    private static async Task<IResult> ListSets(
        IAncestralPortraitService service, HttpContext httpContext, int orderId, CancellationToken cancellationToken)
    {
        var identityId = httpContext.User.GetIdentityId() ?? string.Empty;
        var (response, statusCode) = await service.ListSetsAsync(orderId, identityId, cancellationToken);
        return statusCode switch
        {
            200 => Results.Ok(response),
            403 => Results.Forbid(),
            _ => Results.NotFound(),
        };
    }

    private static async Task<IResult> GetSetById(
        IAncestralPortraitService service, HttpContext httpContext, Guid setId, CancellationToken cancellationToken)
    {
        var identityId = httpContext.User.GetIdentityId() ?? string.Empty;
        var (response, statusCode) = await service.GetSetByIdAsync(setId, identityId, cancellationToken);
        return statusCode switch
        {
            200 => Results.Ok(response),
            403 => Results.Forbid(),
            _ => Results.NotFound(),
        };
    }

    private static async Task<IResult> Generate(
        IAncestralPortraitService service, HttpContext httpContext, Guid setId, CancellationToken cancellationToken)
    {
        var identityId = httpContext.User.GetIdentityId() ?? string.Empty;
        var statusCode = await service.RequestGenerateAsync(setId, identityId, cancellationToken);
        return statusCode switch
        {
            202 => Results.Accepted(),
            403 => Results.Forbid(),
            _ => Results.NotFound(),
        };
    }

    private static async Task<IResult> DeleteSet(
        IAncestralPortraitService service, HttpContext httpContext, Guid setId, CancellationToken cancellationToken)
    {
        var identityId = httpContext.User.GetIdentityId() ?? string.Empty;
        var statusCode = await service.DeleteSetAsync(setId, identityId, cancellationToken);
        return statusCode switch
        {
            200 => Results.NoContent(),
            409 => Results.Conflict(new { Message = "This set is still generating. Try again once it finishes." }),
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

    private static async Task<IResult> Usage(IAncestralPortraitService service, CancellationToken cancellationToken)
        => Results.Ok(await service.GetUsageSummaryAsync(cancellationToken));

    private static async Task<IResult> GetSettings(
        IAncestralPortraitSettingsService service, CancellationToken cancellationToken)
        => Results.Ok(await service.GetAsync(cancellationToken));

    private static async Task<IResult> UpdateSettings(
        AncestralPortraitSettingsContract.Request request,
        IAncestralPortraitSettingsService service,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var problem = request.ValidateAndGetProblem();
        if (problem is not null) return problem;

        var identityId = httpContext.User.GetIdentityId() ?? string.Empty;
        return Results.Ok(await service.UpdateAsync(request, identityId, cancellationToken));
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
