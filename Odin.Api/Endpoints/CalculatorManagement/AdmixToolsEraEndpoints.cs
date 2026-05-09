namespace Odin.Api.Endpoints.CalculatorManagement;

public static class AdmixToolsEraEndpoints
{
    public static void MapAdmixToolsEraEndpoints(this IEndpointRouteBuilder app)
    {
        var endpoints = app.MapGroup("api/admix-tools-eras");

        endpoints.MapGet("/", GetAll)
            .RequireAuthorization("EmailVerified")
            .RequireRateLimiting("authenticated");
    }

    private static async Task<IResult> GetAll(IAdmixToolsEraService service)
    {
        var items = await service.GetAllAsync();
        return Results.Ok(items);
    }
}
