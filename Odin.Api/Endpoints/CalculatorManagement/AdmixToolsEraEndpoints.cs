using Odin.Api.Endpoints.CalculatorManagement.Models;

namespace Odin.Api.Endpoints.CalculatorManagement;

public static class AdmixToolsEraEndpoints
{
    public static void MapAdmixToolsEraEndpoints(this IEndpointRouteBuilder app)
    {
        var endpoints = app.MapGroup("api/admix-tools-eras");

        endpoints.MapGet("/", GetAll)
            .RequireAuthorization("EmailVerified")
            .RequireRateLimiting("authenticated")
            .Produces<IReadOnlyList<GetAdmixToolsEraContract.Response>>(StatusCodes.Status200OK);
    }

    private static async Task<IResult> GetAll(IAdmixToolsEraService service)
    {
        var items = await service.GetAllAsync();
        return Results.Ok(items);
    }
}
