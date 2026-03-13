using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using Odin.Api.Data;
using Odin.Api.Data.Entities;
using Odin.Api.Data.Enums;
using Odin.Api.Endpoints.ReportManagement.Models;
using Odin.Api.Extensions;

namespace Odin.Api.Endpoints.ReportManagement
{
    public static class ReportEndpoints
    {
        public static void MapReportEndpoints(this IEndpointRouteBuilder app)
        {
            var endpoints = app.MapGroup("api/reports");

            endpoints.MapPost("/", Create).RequireAuthorization("Authenticated").DisableAntiforgery();
            endpoints.MapGet("/my", GetMyReports).RequireAuthorization("Authenticated");
            endpoints.MapGet("/", GetAll).RequireAuthorization("AdminOnly");
            endpoints.MapGet("/{id:int}", GetDetail).RequireAuthorization("Authenticated");
            endpoints.MapPatch("/{id:int}/status", UpdateStatus).RequireAuthorization("AdminOnly");
            endpoints.MapGet("/{id:int}/file", DownloadFile).RequireAuthorization("Authenticated");
        }

        private static async Task<IResult> Create(
            IReportService service,
            ApplicationDbContext dbContext,
            HttpContext httpContext,
            IFormFile? file,
            [FromForm] string type,
            [FromForm] string subject,
            [FromForm] string description,
            [FromForm] string? pageUrl)
        {
            var userId = await ResolveUserId(httpContext, dbContext);
            if (userId is null) return Results.Unauthorized();

            var request = new CreateReportContract.Request
            {
                Type = type,
                Subject = subject,
                Description = description,
                PageUrl = pageUrl
            };

            var validationProblem = request.ValidateAndGetProblem();
            if (validationProblem is not null) return validationProblem;

            try
            {
                var result = await service.CreateAsync(userId.Value, request, file);
                return Results.Created($"/api/reports/{result.Id}", result);
            }
            catch (InvalidOperationException ex)
            {
                return Results.Problem(ex.Message, statusCode: 400);
            }
        }

        private static async Task<IResult> GetMyReports(
            IReportService service,
            ApplicationDbContext dbContext,
            HttpContext httpContext,
            int page = 1,
            int pageSize = 20)
        {
            var userId = await ResolveUserId(httpContext, dbContext);
            if (userId is null) return Results.Unauthorized();

            var reports = await service.GetUserReportsAsync(userId.Value, page, pageSize);
            return Results.Ok(reports);
        }

        private static async Task<IResult> GetAll(
            IReportService service,
            int page = 1,
            int pageSize = 20,
            string? type = null,
            string? status = null)
        {
            ReportType? typeFilter = null;
            ReportStatus? statusFilter = null;

            if (type is not null && Enum.TryParse<ReportType>(type, ignoreCase: true, out var parsedType))
                typeFilter = parsedType;

            if (status is not null && Enum.TryParse<ReportStatus>(status, ignoreCase: true, out var parsedStatus))
                statusFilter = parsedStatus;

            var reports = await service.GetAllReportsAsync(page, pageSize, typeFilter, statusFilter);
            return Results.Ok(reports);
        }

        private static async Task<IResult> GetDetail(
            IReportService service,
            ApplicationDbContext dbContext,
            HttpContext httpContext,
            int id)
        {
            var userId = await ResolveUserId(httpContext, dbContext);
            if (userId is null) return Results.Unauthorized();

            var isAdmin = await IsAdmin(httpContext, dbContext);
            var report = await service.GetReportDetailAsync(id, isAdmin ? null : userId);

            return report is not null ? Results.Ok(report) : Results.NotFound();
        }

        private static async Task<IResult> UpdateStatus(
            IReportService service,
            UpdateReportStatusContract.Request request,
            int id)
        {
            var validationProblem = request.ValidateAndGetProblem();
            if (validationProblem is not null) return validationProblem;

            var result = await service.UpdateStatusAsync(id, request);
            return result is not null ? Results.Ok(result) : Results.NotFound();
        }

        private static async Task<IResult> DownloadFile(
            IReportService service,
            ApplicationDbContext dbContext,
            HttpContext httpContext,
            int id)
        {
            var userId = await ResolveUserId(httpContext, dbContext);
            if (userId is null) return Results.Unauthorized();

            var isAdmin = await IsAdmin(httpContext, dbContext);
            var file = await service.GetFileAsync(id, isAdmin ? null : userId);

            if (file is null) return Results.NotFound();

            return Results.File(file.Value.data, file.Value.contentType, file.Value.fileName);
        }

        private static async Task<int?> ResolveUserId(HttpContext httpContext, ApplicationDbContext dbContext)
        {
            var identityId = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier)
                             ?? httpContext.User.FindFirstValue("sub");

            if (string.IsNullOrEmpty(identityId)) return null;

            var user = await dbContext.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.IdentityId == identityId);

            return user?.Id;
        }

        private static async Task<bool> IsAdmin(HttpContext httpContext, ApplicationDbContext dbContext)
        {
            var identityId = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier)
                             ?? httpContext.User.FindFirstValue("sub");

            if (string.IsNullOrEmpty(identityId)) return false;

            var user = await dbContext.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.IdentityId == identityId);

            return user?.Role == AppRole.Admin;
        }
    }
}
