using Microsoft.AspNetCore.Mvc;
using Odin.Api.Endpoints.UserManagement.Models;
using Odin.Api.Extensions;

namespace Odin.Api.Endpoints.UserManagement
{
    public static class UserEndpoints
    {
        public static void MapUserEndpoints(this IEndpointRouteBuilder app)
        {
            var endpoints = app.MapGroup("api/users");

            endpoints.MapGet("/", GetUsers).RequireAuthorization("AdminOnly");
            endpoints.MapPost("/", CreateUser).RequireAuthorization("Authenticated");
            endpoints.MapGet("/{identityId}", GetUserByIdentityId).RequireAuthorization("Authenticated");
            endpoints.MapPut("/{identityId}", UpdateUser).RequireAuthorization("Authenticated");
            endpoints.MapDelete("/{identityId}", DeleteUser).RequireAuthorization("AdminOnly");
            endpoints.MapGet("/ethnicities", GetEthnicities).RequireAuthorization("Authenticated");
            endpoints.MapGet("/eras", GetEras).RequireAuthorization("Authenticated");
            endpoints.MapPatch("/{identityId}/role", UpdateUserRole).RequireAuthorization("AdminOnly");
        }

        private static IResult GetUsers()
        {
            return Results.Ok("User endpoint is working!");
        }

        private static async Task<IResult> CreateUser(IUserService userService, [FromBody]CreateUserContract.Request request)
        {
            var validationProblem = request.ValidateAndGetProblem();
            if (validationProblem is not null)
            {
                return validationProblem;
            }

            var response = await userService.CreateUserAsync(request);
            return Results.Ok(response);
        }

        private static async Task<IResult> GetUserByIdentityId(IUserService userService, string identityId)
        {
            var response = await userService.GetUserByIdentityIdAsync(identityId);
            return response is not null ? Results.Ok(response) : Results.NotFound();
        }

        private static async Task<IResult> UpdateUser(IUserService userService, string identityId, [FromBody]UpdateUserContract.Request request)
        {
            var validationProblem = request.ValidateAndGetProblem();
            if (validationProblem is not null)
            {
                return validationProblem;
            }

            var response = await userService.UpdateUserAsync(identityId, request);
            return response is not null ? Results.Ok(response) : Results.NotFound();
        }

        private static async Task<IResult> DeleteUser(IUserService userService, string identityId)
        {
            var deleted = await userService.DeleteUserAsync(identityId);
            return deleted ? Results.NoContent() : Results.NotFound();
        }

        private static async Task<IResult> GetEthnicities(IEthnicityService ethnicityService)
        {
            var ethnicities = await ethnicityService.GetAllAsync();
            return Results.Ok(ethnicities);
        }

        private static async Task<IResult> GetEras(IEraService eraService)
        {
            var eras = await eraService.GetAllAsync();
            return Results.Ok(eras);
        }

        private static async Task<IResult> UpdateUserRole(
            IUserService userService,
            string identityId,
            [FromBody] Models.UpdateUserRoleContract.Request request)
        {
            var validationProblem = request.ValidateAndGetProblem();
            if (validationProblem is not null)
            {
                return validationProblem;
            }

            var response = await userService.UpdateUserRoleAsync(identityId, request);
            return response is not null ? Results.Ok(response) : Results.NotFound();
        }
    }
}
