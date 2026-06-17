using System.Net.Mail;
using Odin.Api.Endpoints.Subscribe.Models;
using Odin.Api.Services.Email;

namespace Odin.Api.Endpoints.Subscribe;

/// <summary>
/// Public, anonymous pre-launch waitlist signup. While self-service registration is disabled in
/// production, the marketing site collects interested emails here and forwards them to the configured
/// Resend Audience (<see cref="IResendAudienceService"/>) so we can notify everyone when we open.
/// No DB entity — Resend is the store of record for the waitlist.
/// </summary>
public static class SubscribeEndpoints
{
    public static void MapSubscribeEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("api/public/subscribe")
            .AllowAnonymous()
            .RequireRateLimiting("strict")
            .WithTags("Subscribe");

        group.MapPost("/", Join)
            .WithSummary("Adds an email to the pre-launch waitlist (Resend Audience).")
            .Produces<SubscribeResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest);
    }

    private static async Task<IResult> Join(
        SubscribeRequest request,
        IResendAudienceService audience,
        ILogger<SubscribeMarker> logger,
        CancellationToken ct)
    {
        var email = request.Email?.Trim() ?? "";
        if (!IsValidEmail(email))
            return Results.BadRequest(new { error = "A valid email address is required." });

        try
        {
            await audience.AddContactAsync(email, firstName: null, lastName: null, ct);
        }
        catch (Exception ex)
        {
            // Never surface infrastructure errors to an anonymous visitor — the address is what matters,
            // and we don't want to leak Resend internals or hand attackers a probe. Log and report success.
            logger.LogError(ex, "Failed to add waitlist contact to Resend audience.");
        }

        return Results.Ok(new SubscribeResponse(true));
    }

    private static bool IsValidEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email) || email.Length > 320)
            return false;
        return MailAddress.TryCreate(email, out _);
    }

    /// <summary>Marker type so the endpoint can resolve a category-named <see cref="ILogger{T}"/>.</summary>
    private sealed class SubscribeMarker;
}
