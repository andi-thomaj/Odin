using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Odin.Api.Data;
using Odin.Api.Endpoints.UserManagement.Models;

namespace Odin.Api.Endpoints.UserManagement
{
    public interface IEthnicityService
    {
        Task<IEnumerable<GetEthnicitiesContract.Response>> GetAllAsync();
    }

    public class EthnicityService(ApplicationDbContext dbContext, IMemoryCache cache) : IEthnicityService
    {
        private const string CacheKey = "AllEthnicities";
        private static readonly TimeSpan CacheDuration = TimeSpan.FromHours(1);

        public async Task<IEnumerable<GetEthnicitiesContract.Response>> GetAllAsync()
        {
            if (cache.TryGetValue(CacheKey, out List<GetEthnicitiesContract.Response>? cached))
                return cached!;

            var result = await dbContext.Ethnicities
                .AsNoTracking()
                .Include(e => e.Regions)
                .Select(e => new GetEthnicitiesContract.Response
                {
                    Id = e.Id,
                    Name = e.Name,
                    Regions = e.Regions.Select(r => new GetEthnicitiesContract.RegionItem
                    {
                        Id = r.Id, Name = r.Name
                    }).ToList()
                })
                .ToListAsync();

            cache.Set(CacheKey, result, new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = CacheDuration
            });

            return result;
        }
    }
}
