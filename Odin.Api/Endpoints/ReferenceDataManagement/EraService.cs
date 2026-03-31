using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Hosting;
using Odin.Api.Data;
using Odin.Api.Endpoints.UserManagement.Models;

namespace Odin.Api.Endpoints.ReferenceDataManagement;

public interface IEraService
{
    Task<IEnumerable<GetErasContract.Response>> GetAllAsync();
}

public class EraService(
    ApplicationDbContext dbContext,
    IMemoryCache cache,
    IHostEnvironment hostEnvironment) : IEraService
{
    private const string CacheKey = "AllEras";
    private static readonly TimeSpan CacheDuration = TimeSpan.FromHours(1);

    public async Task<IEnumerable<GetErasContract.Response>> GetAllAsync()
    {
        if (!hostEnvironment.IsEnvironment("Testing") &&
            cache.TryGetValue(CacheKey, out List<GetErasContract.Response>? cached))
            return cached!;

        var result = await dbContext.Eras
            .AsNoTracking()
            .AsSplitQuery()
            .Include(e => e.Populations)
            .Select(e => new GetErasContract.Response
            {
                Id = e.Id,
                Name = e.Name,
                Description = e.Description,
                Populations = e.Populations.Select(p => new GetErasContract.PopulationItem
                {
                    Id = p.Id,
                    Name = p.Name,
                    Description = p.Description,
                }).ToList()
            })
            .ToListAsync();

        if (!hostEnvironment.IsEnvironment("Testing"))
        {
            cache.Set(CacheKey, result, new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = CacheDuration
            });
        }

        return result;
    }
}
