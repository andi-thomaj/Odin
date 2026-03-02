using Microsoft.EntityFrameworkCore;
using Odin.Api.Data;
using Odin.Api.Endpoints.UserManagement.Models;

namespace Odin.Api.Endpoints.UserManagement
{
    public interface IEthnicityService
    {
        Task<IEnumerable<GetEthnicitiesContract.Response>> GetAllAsync();
    }

    public class EthnicityService(ApplicationDbContext dbContext) : IEthnicityService
    {
        public async Task<IEnumerable<GetEthnicitiesContract.Response>> GetAllAsync()
        {
            return await dbContext.Ethnicities
                .AsNoTracking()
                .Include(e => e.Regions)
                .Select(e => new GetEthnicitiesContract.Response
                {
                    Id = e.Id,
                    Name = e.Name,
                    Regions = e.Regions.Select(r => new GetEthnicitiesContract.RegionItem
                    {
                        Id = r.Id,
                        Name = r.Name
                    }).ToList()
                })
                .ToListAsync();
        }
    }
}
