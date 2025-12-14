namespace Odin.Api.Endpoints.UserManagement
{
    public static class UserEndpoints
    {
        public static void MapUserEndpoints(this IEndpointRouteBuilder app)
        {
            var endpoints = app.MapGroup("api/users");

            endpoints.MapGet("/", () => Results.Ok("User endpoint is working!"));
        }
    }
}
