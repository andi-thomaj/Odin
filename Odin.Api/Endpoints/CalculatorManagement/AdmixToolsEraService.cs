using Microsoft.EntityFrameworkCore;
using Odin.Api.Data;
using Odin.Api.Endpoints.CalculatorManagement.Models;

namespace Odin.Api.Endpoints.CalculatorManagement;

public interface IAdmixToolsEraService
{
    Task<IReadOnlyList<GetAdmixToolsEraContract.Response>> GetAllAsync(CancellationToken ct = default);
}

public class AdmixToolsEraService(ApplicationDbContext dbContext) : IAdmixToolsEraService
{
    public async Task<IReadOnlyList<GetAdmixToolsEraContract.Response>> GetAllAsync(CancellationToken ct = default)
    {
        return await dbContext.AdmixToolsEras
            .AsNoTracking()
            .OrderBy(e => e.Id)
            .Select(e => new GetAdmixToolsEraContract.Response
            {
                Id = e.Id,
                Name = e.Name
            })
            .ToListAsync(ct);
    }
}
