using Odin.Api.Extensions;

namespace Odin.Api.Endpoints.AuthRegistration;

public static class AuthRegistrationEndpoints
{
    public static void MapAuthRegistrationEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("api/auth");
        group.MapPost("/register", Register)
            .AllowAnonymous()
            .RequireRateLimiting("registration");
    }

    private static async Task<IResult> Register(
        IAuthRegistrationService registrationService,
        HttpContext httpContext,
        [Microsoft.AspNetCore.Mvc.FromBody] RegisterContract.Request request,
        CancellationToken cancellationToken)
    {
        var validationProblem = request.ValidateAndGetProblem();
        if (validationProblem is not null)
            return validationProblem;

        var ipAddress = httpContext.Request.Headers["X-Forwarded-For"].FirstOrDefault()?.Split(',')[0].Trim()
                        ?? httpContext.Connection.RemoteIpAddress?.ToString();

        return await registrationService.RegisterAsync(request, ipAddress, cancellationToken);
    }
}
