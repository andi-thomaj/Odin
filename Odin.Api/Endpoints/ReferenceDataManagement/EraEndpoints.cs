using Odin.Api.Endpoints.UserManagement.Models;

namespace Odin.Api.Endpoints.ReferenceDataManagement
{
    public static class EraEndpoints
    {
        public static void MapEraEndpoints(this IEndpointRouteBuilder app)
        {
            var endpoints = app.MapGroup("api/eras");

            endpoints.MapGet("/", GetAll)
                .AllowAnonymous()
                .Produces<IEnumerable<GetErasContract.Response>>(StatusCodes.Status200OK);
        }

        private static async Task<IResult> GetAll(IEraService eraService)
        {
            var eras = await eraService.GetAllAsync();
            return Results.Ok(eras);
        }
    }
}
