using Odin.Api.Endpoints.UserManagement;

namespace Odin.Api.Endpoints.ReferenceDataManagement
{
    public static class EraEndpoints
    {
        public static void MapEraEndpoints(this IEndpointRouteBuilder app)
        {
            var endpoints = app.MapGroup("api/eras");

            endpoints.MapGet("/", GetAll)
                .RequireAuthorization("Authenticated")
                .RequireRateLimiting("authenticated");
        }

        private static async Task<IResult> GetAll(IEraService eraService)
        {
            var eras = await eraService.GetAllAsync();
            return Results.Ok(eras);
        }
    }
}
