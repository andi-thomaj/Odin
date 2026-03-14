using Odin.Api.Endpoints.UserManagement;

namespace Odin.Api.Endpoints.ReferenceDataManagement
{
    public static class EthnicityEndpoints
    {
        public static void MapEthnicityEndpoints(this IEndpointRouteBuilder app)
        {
            var endpoints = app.MapGroup("api/ethnicities");

            endpoints.MapGet("/", GetAll).RequireAuthorization("Authenticated");
        }

        private static async Task<IResult> GetAll(IEthnicityService ethnicityService)
        {
            var ethnicities = await ethnicityService.GetAllAsync();
            return Results.Ok(ethnicities);
        }
    }
}
