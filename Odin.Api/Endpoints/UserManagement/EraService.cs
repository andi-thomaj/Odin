using Microsoft.EntityFrameworkCore;
using Odin.Api.Data;
using Odin.Api.Endpoints.UserManagement.Models;

namespace Odin.Api.Endpoints.UserManagement
{
    public interface IEraService
    {
        Task<IEnumerable<GetErasContract.Response>> GetAllAsync();
    }

    public class EraService(ApplicationDbContext dbContext) : IEraService
    {
        public async Task<IEnumerable<GetErasContract.Response>> GetAllAsync()
        {
            return await dbContext.Eras
                .AsNoTracking()
                .Include(e => e.Populations)
                    .ThenInclude(p => p.SubPopulations)
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
                        SubPopulations = p.SubPopulations.Select(sp => new GetErasContract.SubPopulationItem
                        {
                            Id = sp.Id,
                            Name = sp.Name,
                            Description = sp.Description
                        }).ToList()
                    }).ToList()
                })
                .ToListAsync();
        }
    }
}
