using Odin.Api.Endpoints.UserManagement.Models;
using Odin.Api.Extensions;

namespace Odin.Api.Endpoints.UserManagement
{
    public static class UserEndpoints
    {
        public static void MapUserEndpoints(this IEndpointRouteBuilder app)
        {
            var endpoints = app.MapGroup("api/users");

            endpoints.MapGet("/", GetUsers);
            endpoints.MapPost("/", CreateUser);
        }

        private static IResult GetUsers()
        {
            return Results.Ok("User endpoint is working!");
        }

        private static async Task<IResult> CreateUser(CreateUserContract.Request request)
        {
            var validationProblem = request.ValidateAndGetProblem();
            if (validationProblem is not null)
            {
                return validationProblem;
            }

            return Results.Ok();
        }
    }
}
