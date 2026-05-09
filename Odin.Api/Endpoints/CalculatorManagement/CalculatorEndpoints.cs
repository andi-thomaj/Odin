using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Odin.Api.Authentication;
using Odin.Api.Data;
using Odin.Api.Data.Entities;
using Odin.Api.Endpoints.CalculatorManagement.Models;

namespace Odin.Api.Endpoints.CalculatorManagement;

public static class CalculatorEndpoints
{
    public static void MapCalculatorEndpoints(this IEndpointRouteBuilder app)
    {
        var endpoints = app.MapGroup("api/calculators");

        endpoints.MapGet("/", GetAll)
            .RequireAuthorization("EmailVerified")
            .RequireRateLimiting("authenticated");

        endpoints.MapGet("/visible", GetVisible)
            .RequireAuthorization("EmailVerified")
            .RequireRateLimiting("authenticated");

        endpoints.MapGet("/{id:int}", GetById)
            .RequireAuthorization("EmailVerified")
            .RequireRateLimiting("authenticated");

        endpoints.MapPost("/", Create)
            .RequireAuthorization("EmailVerified")
            .RequireRateLimiting("strict");

        endpoints.MapPut("/{id:int}", Update)
            .RequireAuthorization("EmailVerified")
            .RequireRateLimiting("strict");

        endpoints.MapDelete("/{id:int}", Delete)
            .RequireAuthorization("EmailVerified")
            .RequireRateLimiting("strict");
    }

    private static async Task<IResult> GetAll(ICalculatorService service)
    {
        var items = await service.GetAdminCalculatorsAsync();
        return Results.Ok(items);
    }

    private static async Task<IResult> GetVisible(
        HttpContext httpContext,
        ApplicationDbContext dbContext,
        ICalculatorService service)
    {
        var ctx = await ResolveUserContextAsync(httpContext, dbContext);
        if (ctx is null) return Results.Unauthorized();

        var items = await service.GetVisibleCalculatorsAsync(ctx.UserId, ctx.IsAdmin);
        return Results.Ok(items);
    }

    private static async Task<IResult> GetById(
        HttpContext httpContext,
        ApplicationDbContext dbContext,
        ICalculatorService service,
        int id)
    {
        var ctx = await ResolveUserContextAsync(httpContext, dbContext);
        if (ctx is null) return Results.Unauthorized();

        var item = await service.GetByIdAsync(id, ctx.UserId, ctx.IsAdmin);
        return item is null ? Results.NotFound() : Results.Ok(item);
    }

    private static async Task<IResult> Create(
        HttpContext httpContext,
        ApplicationDbContext dbContext,
        ICalculatorService service,
        CreateCalculatorContract.Request request)
    {
        var ctx = await ResolveUserContextAsync(httpContext, dbContext);
        if (ctx is null) return Results.Unauthorized();

        var (response, error) = await service.CreateAsync(request, ctx.UserId, ctx.IsAdmin, ctx.IdentityId);
        if (error is not null) return Results.BadRequest(error);
        return Results.Created($"/api/calculators/{response!.Id}", response);
    }

    private static async Task<IResult> Update(
        HttpContext httpContext,
        ApplicationDbContext dbContext,
        ICalculatorService service,
        int id,
        UpdateCalculatorContract.Request request)
    {
        var ctx = await ResolveUserContextAsync(httpContext, dbContext);
        if (ctx is null) return Results.Unauthorized();

        var (response, error, notFound, forbidden) = await service.UpdateAsync(id, request, ctx.UserId, ctx.IsAdmin, ctx.IdentityId);
        if (notFound) return Results.NotFound();
        if (forbidden) return Results.Forbid();
        if (error is not null) return Results.BadRequest(error);
        return Results.Ok(response);
    }

    private static async Task<IResult> Delete(
        HttpContext httpContext,
        ApplicationDbContext dbContext,
        ICalculatorService service,
        int id)
    {
        var ctx = await ResolveUserContextAsync(httpContext, dbContext);
        if (ctx is null) return Results.Unauthorized();

        var (deleted, error, forbidden) = await service.DeleteAsync(id, ctx.UserId, ctx.IsAdmin);
        if (forbidden) return Results.Forbid();
        if (error is not null) return Results.BadRequest(error);
        return deleted ? Results.NoContent() : Results.NotFound();
    }

    private sealed record CurrentUserContext(int UserId, bool IsAdmin, string IdentityId);

    private static async Task<CurrentUserContext?> ResolveUserContextAsync(HttpContext httpContext, ApplicationDbContext dbContext)
    {
        var identityId = httpContext.User.GetIdentityId();
        if (string.IsNullOrEmpty(identityId)) return null;

        var user = await dbContext.Users
            .AsNoTracking()
            .Where(u => u.IdentityId == identityId)
            .Select(u => new { u.Id, u.Role })
            .FirstOrDefaultAsync();

        if (user is null) return null;
        return new CurrentUserContext(user.Id, user.Role == AppRole.Admin, identityId);
    }
}
