using System.ComponentModel.DataAnnotations;
using Odin.Api.Endpoints.UserManagement.Models;

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
            var validationResults = request.Validate(new ValidationContext(request)).ToList();
            if (validationResults.Count != 0)
            {
                var errors = validationResults
                    .SelectMany(vr => vr.MemberNames.Select(mn => new { MemberName = mn, vr.ErrorMessage }))
                    .GroupBy(x => x.MemberName)
                    .ToDictionary(
                        g => g.Key,
                        g => g.Select(x => x.ErrorMessage ?? string.Empty).ToArray());

                return Results.ValidationProblem(errors);
            }

            await Task.Delay(2000);
            return Results.Ok();
        }
    }
}
