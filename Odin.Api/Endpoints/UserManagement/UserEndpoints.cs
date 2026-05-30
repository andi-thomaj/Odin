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

            endpoints.MapGet("/", ListUsers)
                .RequireAuthorization("AdminOnly")
                .RequireRateLimiting("strict")
                .Produces<ListUsersContract.Response>(StatusCodes.Status200OK);

            endpoints.MapPost("/", CreateUser)
                .RequireAuthorization("Authenticated")
                .RequireRateLimiting("authenticated")
                .Produces<CreateUserContract.Response>(StatusCodes.Status200OK);

            endpoints.MapGet("/{identityId}", GetUserByIdentityId)
                .RequireAuthorization("EmailVerified")
                .RequireRateLimiting("authenticated")
                .Produces<GetUserContract.Response>(StatusCodes.Status200OK);

            endpoints.MapPut("/{identityId}", UpdateUser)
                .RequireAuthorization("EmailVerified")
                .RequireRateLimiting("authenticated")
                .Produces<UpdateUserContract.Response>(StatusCodes.Status200OK);

            endpoints.MapDelete("/{identityId}", DeleteUser)
                .RequireAuthorization("AdminOnly")
                .RequireRateLimiting("strict")
                .Produces(StatusCodes.Status204NoContent);

            endpoints.MapPatch("/{identityId}/role", UpdateUserRole)
                .RequireAuthorization("AdminOnly")
                .RequireRateLimiting("strict")
                .Produces<UpdateUserRoleContract.Response>(StatusCodes.Status200OK);
        }

        private static async Task<IResult> ListUsers(IUserService userService, int skip = 0, int take = 50)
        {
            skip = Math.Max(0, skip);
            take = Math.Clamp(take, 1, 100);
            var result = await userService.ListUsersAsync(skip, take);
            return Results.Ok(result);
        }

        private static async Task<IResult> CreateUser(IUserService userService,
            HttpContext httpContext,
            [FromBody] CreateUserContract.Request request)
        {
            var validationProblem = request.ValidateAndGetProblem();
            if (validationProblem is not null)
            {
                return validationProblem;
            }

            var ipAddress = httpContext.Request.Headers["X-Forwarded-For"].FirstOrDefault()?.Split(',')[0].Trim()
                            ?? httpContext.Connection.RemoteIpAddress?.ToString();
            var response = await userService.CreateUserAsync(request, ipAddress);
            return Results.Ok(response);
        }

        private static async Task<IResult> GetUserByIdentityId(IUserService userService, string identityId)
        {
            var response = await userService.GetUserByIdentityIdAsync(identityId);
            return response is not null ? Results.Ok(response) : Results.NotFound();
        }

        private static async Task<IResult> UpdateUser(IUserService userService, string identityId,
            [FromBody] UpdateUserContract.Request request)
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
