using Odin.Api.Endpoints.ReferenceDataManagement.Models;
using Odin.Api.Endpoints.UserManagement.Models;

namespace Odin.Api.Endpoints.ReferenceDataManagement
{
    public static class EthnicityEndpoints
    {
        public static void MapEthnicityEndpoints(this IEndpointRouteBuilder app)
        {
            var endpoints = app.MapGroup("api/ethnicities");

            endpoints.MapGet("/", GetAll)
                .RequireAuthorization("EmailVerified")
                .RequireRateLimiting("authenticated")
                .Produces<IEnumerable<GetEthnicitiesContract.Response>>(StatusCodes.Status200OK);

            endpoints.MapGet("/admin", GetAllAdmin)
                .RequireAuthorization("AdminOnly")
                .RequireRateLimiting("authenticated")
                .Produces<IReadOnlyList<GetEthnicityAdminContract.Response>>(StatusCodes.Status200OK);

            endpoints.MapGet("/admin/{id:int}", GetByIdAdmin)
                .RequireAuthorization("AdminOnly")
                .RequireRateLimiting("authenticated")
                .Produces<GetEthnicityAdminContract.Response>(StatusCodes.Status200OK);

            endpoints.MapPost("/", CreateEthnicity)
                .RequireAuthorization("AdminOnly")
                .RequireRateLimiting("strict")
                .Produces<GetEthnicityAdminContract.Response>(StatusCodes.Status201Created);

            endpoints.MapPut("/{id:int}", UpdateEthnicity)
                .RequireAuthorization("AdminOnly")
                .RequireRateLimiting("strict")
                .Produces<GetEthnicityAdminContract.Response>(StatusCodes.Status200OK);

            endpoints.MapDelete("/{id:int}", DeleteEthnicity)
                .RequireAuthorization("AdminOnly")
                .RequireRateLimiting("strict")
                .Produces(StatusCodes.Status204NoContent);

            endpoints.MapPost("/{ethnicityId:int}/regions", CreateRegion)
                .RequireAuthorization("AdminOnly")
                .RequireRateLimiting("strict")
                .Produces<GetEthnicityAdminContract.RegionItem>(StatusCodes.Status201Created);

            endpoints.MapPut("/{ethnicityId:int}/regions/{regionId:int}", UpdateRegion)
                .RequireAuthorization("AdminOnly")
                .RequireRateLimiting("strict")
                .Produces<GetEthnicityAdminContract.RegionItem>(StatusCodes.Status200OK);

            endpoints.MapDelete("/{ethnicityId:int}/regions/{regionId:int}", DeleteRegion)
                .RequireAuthorization("AdminOnly")
                .RequireRateLimiting("strict")
                .Produces(StatusCodes.Status204NoContent);
        }

        private static async Task<IResult> GetAll(IEthnicityService ethnicityService)
        {
            var ethnicities = await ethnicityService.GetAllAsync();
            return Results.Ok(ethnicities);
        }

        private static async Task<IResult> GetAllAdmin(IEthnicityService ethnicityService)
        {
            var ethnicities = await ethnicityService.GetAllAdminAsync();
            return Results.Ok(ethnicities);
        }

        private static async Task<IResult> GetByIdAdmin(IEthnicityService ethnicityService, int id)
        {
            var ethnicity = await ethnicityService.GetByIdAdminAsync(id);
            return ethnicity is null ? Results.NotFound() : Results.Ok(ethnicity);
        }

        private static async Task<IResult> CreateEthnicity(IEthnicityService ethnicityService, CreateEthnicityContract.Request request)
        {
            var (response, error) = await ethnicityService.CreateEthnicityAsync(request);
            if (error is not null) return Results.BadRequest(error);
            return Results.Created($"/api/ethnicities/admin/{response!.Id}", response);
        }

        private static async Task<IResult> UpdateEthnicity(IEthnicityService ethnicityService, int id, UpdateEthnicityContract.Request request)
        {
            var (response, error, notFound) = await ethnicityService.UpdateEthnicityAsync(id, request);
            if (notFound) return Results.NotFound();
            if (error is not null) return Results.BadRequest(error);
            return Results.Ok(response);
        }

        private static async Task<IResult> DeleteEthnicity(IEthnicityService ethnicityService, int id)
        {
            var ok = await ethnicityService.DeleteEthnicityAsync(id);
            return ok ? Results.NoContent() : Results.NotFound();
        }

        private static async Task<IResult> CreateRegion(IEthnicityService ethnicityService, int ethnicityId, CreateRegionContract.Request request)
        {
            var (region, error, ethnicityNotFound) = await ethnicityService.CreateRegionAsync(ethnicityId, request);
            if (ethnicityNotFound) return Results.NotFound();
            if (error is not null) return Results.BadRequest(error);
            return Results.Created($"/api/ethnicities/admin/{ethnicityId}", region);
        }

        private static async Task<IResult> UpdateRegion(IEthnicityService ethnicityService, int ethnicityId, int regionId, UpdateRegionContract.Request request)
        {
            var (region, error, notFound) = await ethnicityService.UpdateRegionAsync(ethnicityId, regionId, request);
            if (notFound) return Results.NotFound();
            if (error is not null) return Results.BadRequest(error);
            return Results.Ok(region);
        }

        private static async Task<IResult> DeleteRegion(IEthnicityService ethnicityService, int ethnicityId, int regionId)
        {
            var ok = await ethnicityService.DeleteRegionAsync(ethnicityId, regionId);
            return ok ? Results.NoContent() : Results.NotFound();
        }
    }
}
