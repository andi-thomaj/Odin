using Microsoft.EntityFrameworkCore;
using Odin.Api.Data;

namespace Odin.Api.Endpoints.MapImageManagement;

public static class MapImageEndpoints
{
    public static void MapMapImageEndpoints(this IEndpointRouteBuilder app)
    {
        var endpoints = app.MapGroup("api/map-images");

        endpoints.MapGet("/", GetAll)
            .AllowAnonymous()
            .RequireRateLimiting("authenticated");
    }

    private static async Task<IResult> GetAll(ApplicationDbContext db, HttpContext httpContext)
    {
        var baseUrl = $"{httpContext.Request.Scheme}://{httpContext.Request.Host}";

        var maps = await db.MapImages
            .OrderBy(m => m.Name)
            .Select(m => new MapImageResponse
            {
                Id = m.Id,
                Name = m.Name,
                FileName = m.FileName,
                Width = m.Width,
                Height = m.Height,
                Url = $"{baseUrl}/maps/{m.FileName}",
            })
            .ToListAsync();

        return Results.Ok(maps);
    }
}

public class MapImageResponse
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public int Width { get; set; }
    public int Height { get; set; }
    public string Url { get; set; } = string.Empty;
}
