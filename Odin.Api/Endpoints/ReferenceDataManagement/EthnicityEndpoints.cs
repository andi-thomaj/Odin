using Odin.Api.Endpoints.ReferenceDataManagement.Models;

namespace Odin.Api.Endpoints.ReferenceDataManagement
{
    public static class EthnicityEndpoints
    {
        public static void MapEthnicityEndpoints(this IEndpointRouteBuilder app)
        {
            var endpoints = app.MapGroup("api/ethnicities");

            endpoints.MapGet("/", GetAll)
                .RequireAuthorization("EmailVerified")
                .RequireRateLimiting("authenticated");

            endpoints.MapGet("/admin", GetAllAdmin)
                .RequireAuthorization("AdminOnly")
                .RequireRateLimiting("authenticated");

            endpoints.MapGet("/admin/{id:int}", GetByIdAdmin)
                .RequireAuthorization("AdminOnly")
                .RequireRateLimiting("authenticated");

            endpoints.MapPost("/", CreateEthnicity)
                .RequireAuthorization("AdminOnly")
                .RequireRateLimiting("strict");

            endpoints.MapPut("/{id:int}", UpdateEthnicity)
                .RequireAuthorization("AdminOnly")
                .RequireRateLimiting("strict");

            endpoints.MapDelete("/{id:int}", DeleteEthnicity)
                .RequireAuthorization("AdminOnly")
                .RequireRateLimiting("strict");

            endpoints.MapPost("/{ethnicityId:int}/regions", CreateRegion)
                .RequireAuthorization("AdminOnly")
                .RequireRateLimiting("strict");

            endpoints.MapPut("/{ethnicityId:int}/regions/{regionId:int}", UpdateRegion)
                .RequireAuthorization("AdminOnly")
                .RequireRateLimiting("strict");

            endpoints.MapDelete("/{ethnicityId:int}/regions/{regionId:int}", DeleteRegion)
                .RequireAuthorization("AdminOnly")
                .RequireRateLimiting("strict");
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
