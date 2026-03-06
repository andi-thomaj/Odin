using Microsoft.EntityFrameworkCore;
using Odin.Api.Data;
using Odin.Api.Data.Entities;
using Odin.Api.Endpoints.GeneticInspectionManagement.Models;
using Odin.Api.Endpoints.RawGeneticFileManagement.Models;

namespace Odin.Api.Endpoints.GeneticInspectionManagement
{
    public interface IGeneticInspectionService
    {
        Task<CreateGeneticInspectionContract.Response> CreateAsync(CreateGeneticInspectionContract.Request request);
        Task<GetGeneticInspectionContract.Response?> GetByIdAsync(int id);
        Task<IEnumerable<GetGeneticInspectionContract.Response>> GetAllAsync();
        Task<bool> DeleteAsync(int id);
        Task<UploadGeneticFileContract.Response?> UploadGeneticFileAsync(int inspectionId, UploadGeneticFileContract.Request request);
        Task<(byte[] Data, string FileName)?> DownloadGeneticFileAsync(int inspectionId);
        Task<bool> DeleteGeneticFileAsync(int inspectionId);
    }

    public class GeneticInspectionService(ApplicationDbContext dbContext) : IGeneticInspectionService
    {
        public async Task<CreateGeneticInspectionContract.Response> CreateAsync(CreateGeneticInspectionContract.Request request)
        {
            var geneticInspection = new GeneticInspection
            {
                FirstName = request.FirstName,
                MiddleName = request.MiddleName ?? string.Empty,
                LastName = request.LastName,
                RawGeneticFileId = request.RawGeneticFileId,
                CreatedBy = string.Empty
            };

            dbContext.GeneticInspections.Add(geneticInspection);
            await dbContext.SaveChangesAsync();

            // Add region associations
            var regions = await dbContext.Regions
                .Include(r => r.Ethnicity)
                .Where(r => request.RegionIds.Contains(r.Id))
                .ToListAsync();

            foreach (var region in regions)
            {
                dbContext.GeneticInspectionRegions.Add(new GeneticInspectionRegion
                {
                    GeneticInspectionId = geneticInspection.Id,
                    GeneticInspection = geneticInspection,
                    RegionId = region.Id,
                    Region = region
                });
            }

            await dbContext.SaveChangesAsync();

            return new CreateGeneticInspectionContract.Response
            {
                Id = geneticInspection.Id,
                FirstName = geneticInspection.FirstName,
                MiddleName = geneticInspection.MiddleName,
                LastName = geneticInspection.LastName,
                RawGeneticFileId = geneticInspection.RawGeneticFileId,
                Regions = regions.Select(r => new RegionResponse
                {
                    Id = r.Id,
                    Name = r.Name,
                    EthnicityName = r.Ethnicity.Name
                }).ToList()
            };
        }

        public async Task<GetGeneticInspectionContract.Response?> GetByIdAsync(int id)
        {
            var inspection = await dbContext.GeneticInspections
                .AsNoTracking()
                .Include(gi => gi.RawGeneticFile)
                .Include(gi => gi.GeneticInspectionRegions)
                    .ThenInclude(gir => gir.Region)
                        .ThenInclude(r => r.Ethnicity)
                .FirstOrDefaultAsync(gi => gi.Id == id);

            if (inspection is null)
            {
                return null;
            }

            return new GetGeneticInspectionContract.Response
            {
                Id = inspection.Id,
                FirstName = inspection.FirstName,
                MiddleName = inspection.MiddleName,
                LastName = inspection.LastName,
                RawGeneticFileId = inspection.RawGeneticFileId,
                RawGeneticFileName = inspection.RawGeneticFile.FileName,
                Regions = inspection.GeneticInspectionRegions.Select(gir => new RegionResponse
                {
                    Id = gir.Region.Id,
                    Name = gir.Region.Name,
                    EthnicityName = gir.Region.Ethnicity.Name
                }).ToList()
            };
        }

        public async Task<IEnumerable<GetGeneticInspectionContract.Response>> GetAllAsync()
        {
            return await dbContext.GeneticInspections
                .AsNoTracking()
                .Include(gi => gi.RawGeneticFile)
                .Include(gi => gi.GeneticInspectionRegions)
                    .ThenInclude(gir => gir.Region)
                        .ThenInclude(r => r.Ethnicity)
                .Select(inspection => new GetGeneticInspectionContract.Response
                {
                    Id = inspection.Id,
                    FirstName = inspection.FirstName,
                    MiddleName = inspection.MiddleName,
                    LastName = inspection.LastName,
                    RawGeneticFileId = inspection.RawGeneticFileId,
                    RawGeneticFileName = inspection.RawGeneticFile.FileName,
                    Regions = inspection.GeneticInspectionRegions.Select(gir => new RegionResponse
                    {
                        Id = gir.Region.Id,
                        Name = gir.Region.Name,
                        EthnicityName = gir.Region.Ethnicity.Name
                    }).ToList()
                })
                .ToListAsync();
        }

        public async Task<bool> DeleteAsync(int id)
        {
            var inspection = await dbContext.GeneticInspections.FindAsync(id);

            if (inspection is null)
            {
                return false;
            }

            dbContext.GeneticInspections.Remove(inspection);
            await dbContext.SaveChangesAsync();
            return true;
        }

        public async Task<UploadGeneticFileContract.Response?> UploadGeneticFileAsync(int inspectionId, UploadGeneticFileContract.Request request)
        {
            var inspection = await dbContext.GeneticInspections.FindAsync(inspectionId);

            if (inspection is null)
            {
                return null;
            }

            using var memoryStream = new MemoryStream();
            await request.File.CopyToAsync(memoryStream);

            var rawGeneticFile = new RawGeneticFile
            {
                FileName = request.File.FileName,
                RawData = memoryStream.ToArray(),
                CreatedBy = string.Empty
            };

            dbContext.RawGeneticFiles.Add(rawGeneticFile);
            await dbContext.SaveChangesAsync();

            inspection.RawGeneticFileId = rawGeneticFile.Id;
            await dbContext.SaveChangesAsync();

            return new UploadGeneticFileContract.Response
            {
                Id = rawGeneticFile.Id,
                FileName = rawGeneticFile.FileName,
                FileSize = request.File.Length,
                UploadedAt = DateTime.UtcNow
            };
        }

        public async Task<(byte[] Data, string FileName)?> DownloadGeneticFileAsync(int inspectionId)
        {
            var inspection = await dbContext.GeneticInspections
                .AsNoTracking()
                .Include(gi => gi.RawGeneticFile)
                .FirstOrDefaultAsync(gi => gi.Id == inspectionId);

            if (inspection?.RawGeneticFile is null)
            {
                return null;
            }

            return (inspection.RawGeneticFile.RawData, inspection.RawGeneticFile.FileName);
        }

        public async Task<bool> DeleteGeneticFileAsync(int inspectionId)
        {
            var inspection = await dbContext.GeneticInspections
                .Include(gi => gi.RawGeneticFile)
                .FirstOrDefaultAsync(gi => gi.Id == inspectionId);

            if (inspection?.RawGeneticFile is null)
            {
                return false;
            }

            inspection.RawGeneticFile.IsDeleted = true;
            await dbContext.SaveChangesAsync();
            return true;
        }
    }
}
