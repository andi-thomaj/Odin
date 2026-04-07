using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Odin.Api.Data;
using Odin.Api.Services.Email;
using Odin.Api.Services.LemonSqueezy;

namespace Odin.Api.Endpoints.CheckoutManagement;

public static class CheckoutEndpoints
{
    public static void MapCheckoutEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("api/checkout");

        group.MapPost("/", CreateCheckout)
            .RequireAuthorization("EmailVerified")
            .RequireRateLimiting("strict")
            .WithTags("Checkout")
            .WithSummary("Create a Lemon Squeezy checkout and return the hosted URL");

        group.MapGet("/status", GetCheckoutStatus)
            .RequireAuthorization("EmailVerified")
            .RequireRateLimiting("authenticated")
            .WithTags("Checkout")
            .WithSummary("Check whether the current user has an unused paid credit");
    }

    private static async Task<IResult> CreateCheckout(
        HttpContext httpContext,
        ILemonSqueezyService lemonSqueezyService,
        IOptions<AppPublicOptions> appOptions,
        CancellationToken cancellationToken)
    {
        var identityId = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier)
                         ?? httpContext.User.FindFirstValue("sub")
                         ?? string.Empty;
        var email = httpContext.User.FindFirstValue(ClaimTypes.Email)
                    ?? httpContext.User.FindFirstValue("email");

        var successUrl = $"{appOptions.Value.FrontendBaseUrl.TrimEnd('/')}/dashboard?checkout=success";

        var checkoutUrl = await lemonSqueezyService.CreateCheckoutAsync(
            identityId, email, successUrl, cancellationToken);

        return Results.Ok(new CreateCheckoutResponse(checkoutUrl));
    }

    private static async Task<IResult> GetCheckoutStatus(
        HttpContext httpContext,
        ApplicationDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var identityId = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier)
                         ?? httpContext.User.FindFirstValue("sub")
                         ?? string.Empty;

        var payment = await dbContext.LemonSqueezyPayments
            .AsNoTracking()
            .Where(p => p.UserId == identityId && p.Status == "paid" && p.OrderId == null)
            .OrderByDescending(p => p.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        return Results.Ok(new CheckoutStatusResponse(payment is not null, payment?.Id));
    }
}

public sealed record CreateCheckoutResponse(string CheckoutUrl);
public sealed record CheckoutStatusResponse(bool HasUnusedPayment, int? PaymentId);
