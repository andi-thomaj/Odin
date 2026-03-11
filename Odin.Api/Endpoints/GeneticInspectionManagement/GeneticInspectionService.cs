using Microsoft.EntityFrameworkCore;
using Odin.Api.Data;
using Odin.Api.Data.Entities;
using Odin.Api.Data.Enums;
using Odin.Api.Endpoints.GeneticInspectionManagement.Models;
using Odin.Api.Endpoints.NotificationManagement;
using Odin.Api.Endpoints.RawGeneticFileManagement.Models;

namespace Odin.Api.Endpoints.GeneticInspectionManagement
{
    public interface IGeneticInspectionService
    {
        Task<CreateGeneticInspectionContract.Response> CreateAsync(CreateGeneticInspectionContract.Request request);
        Task<GetGeneticInspectionContract.Response?> GetByIdAsync(int id);
        Task<IEnumerable<GetGeneticInspectionContract.Response>> GetAllAsync();
        Task<bool> DeleteAsync(int id);

        Task<UploadGeneticFileContract.Response?> UploadGeneticFileAsync(int inspectionId,
            UploadGeneticFileContract.Request request);

        Task<(byte[] Data, string FileName)?> DownloadGeneticFileAsync(int inspectionId);
        Task<bool> DeleteGeneticFileAsync(int inspectionId);

        Task<SubmitQpadmResultContract.Response?> SubmitQpadmResultAsync(int inspectionId,
            SubmitQpadmResultContract.Request request);

        Task<SubmitQpadmResultContract.Response?> GetQpadmResultAsync(int inspectionId);
        Task<SubmitVahaduoResultContract.Response?> SubmitVahaduoResultAsync(int inspectionId,
            SubmitVahaduoResultContract.Request request);

        Task<SubmitVahaduoResultContract.Response?> GetVahaduoResultAsync(int inspectionId);
    }

    public class GeneticInspectionService(
        ApplicationDbContext dbContext,
        INotificationService notificationService) : IGeneticInspectionService
    {
        public async Task<CreateGeneticInspectionContract.Response> CreateAsync(
            CreateGeneticInspectionContract.Request request)
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
                    Id = r.Id, Name = r.Name, EthnicityName = r.Ethnicity.Name
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
                    Id = gir.Region.Id, Name = gir.Region.Name, EthnicityName = gir.Region.Ethnicity.Name
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
                        Id = gir.Region.Id, Name = gir.Region.Name, EthnicityName = gir.Region.Ethnicity.Name
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

        public async Task<UploadGeneticFileContract.Response?> UploadGeneticFileAsync(int inspectionId,
            UploadGeneticFileContract.Request request)
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
                FileName = request.File.FileName, RawData = memoryStream.ToArray(), CreatedBy = string.Empty
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

        public async Task<SubmitQpadmResultContract.Response?> SubmitQpadmResultAsync(int inspectionId,
            SubmitQpadmResultContract.Request request)
        {
            var inspection = await dbContext.GeneticInspections
                .Include(gi => gi.Order)
                .Include(gi => gi.User)
                .Include(gi => gi.QpadmResult)
                .ThenInclude(qr => qr!.QpadmResultPopulations)
                .FirstOrDefaultAsync(gi => gi.Id == inspectionId);

            if (inspection is null)
            {
                return null;
            }

            var populationIds = request.Populations.Select(p => p.PopulationId).ToList();
            var populations = await dbContext.Populations
                .Include(p => p.Era)
                .Where(p => populationIds.Contains(p.Id))
                .ToListAsync();

            var percentageLookup = request.Populations.ToDictionary(p => p.PopulationId, p => p.Percentage);

            var perEraTotal = populations
                .GroupBy(p => p.EraId)
                .Where(g => g.Sum(p => percentageLookup.GetValueOrDefault(p.Id)) > 100);

            if (perEraTotal.Any())
            {
                return null;
            }

            var joinEntities = populations.Select(p => new QpadmResultPopulation
            {
                PopulationId = p.Id,
                Percentage = percentageLookup.GetValueOrDefault(p.Id)
            }).ToList();

            if (inspection.QpadmResult is not null)
            {
                inspection.QpadmResult.Weight = request.Weight;
                inspection.QpadmResult.StandardError = request.StandardError;
                inspection.QpadmResult.ZScore = request.ZScore;
                inspection.QpadmResult.PiValue = request.PiValue;
                inspection.QpadmResult.RightSources = request.RightSources;
                inspection.QpadmResult.LeftSources = request.LeftSources;
                inspection.QpadmResult.UpdatedAt = DateTime.UtcNow;
                inspection.QpadmResult.QpadmResultPopulations.Clear();
                foreach (var je in joinEntities)
                {
                    je.QpadmResultId = inspection.QpadmResult.Id;
                    inspection.QpadmResult.QpadmResultPopulations.Add(je);
                }
            }
            else
            {
                inspection.QpadmResult = new QpadmResult
                {
                    GeneticInspectionId = inspectionId,
                    Weight = request.Weight,
                    StandardError = request.StandardError,
                    ZScore = request.ZScore,
                    PiValue = request.PiValue,
                    RightSources = request.RightSources,
                    LeftSources = request.LeftSources,
                    QpadmResultPopulations = joinEntities,
                    CreatedBy = string.Empty
                };
            }

            inspection.Order.Status = Enum.TryParse<OrderStatus>(request.OrderStatus, out var qpadmStatus)
                ? qpadmStatus
                : OrderStatus.InProcess;

            await dbContext.SaveChangesAsync();

            if (inspection.Order.Status == OrderStatus.Completed)
            {
                await notificationService.CreateAndSendAsync(
                    inspection.UserId,
                    NotificationType.OrderCompleted,
                    "Order Completed",
                    $"Your {inspection.Order.Service} analysis results are ready.",
                    inspection.Order.Id.ToString());
            }

            return new SubmitQpadmResultContract.Response
            {
                Id = inspection.QpadmResult.Id,
                GeneticInspectionId = inspectionId,
                Weight = inspection.QpadmResult.Weight,
                StandardError = inspection.QpadmResult.StandardError,
                ZScore = inspection.QpadmResult.ZScore,
                PiValue = inspection.QpadmResult.PiValue,
                RightSources = inspection.QpadmResult.RightSources,
                LeftSources = inspection.QpadmResult.LeftSources,
                Populations = populations.Select(p => new PopulationResponse
                {
                    Id = p.Id,
                    Name = p.Name,
                    EraId = p.EraId,
                    EraName = p.Era.Name,
                    Percentage = percentageLookup.GetValueOrDefault(p.Id)
                }).ToList()
            };
        }

        public async Task<SubmitQpadmResultContract.Response?> GetQpadmResultAsync(int inspectionId)
        {
            var result = await dbContext.QpadmResults
                .AsNoTracking()
                .Include(qr => qr.QpadmResultPopulations)
                .ThenInclude(qrp => qrp.Population)
                .ThenInclude(p => p.Era)
                .FirstOrDefaultAsync(qr => qr.GeneticInspectionId == inspectionId);

            if (result is null)
            {
                return null;
            }

            return new SubmitQpadmResultContract.Response
            {
                Id = result.Id,
                GeneticInspectionId = inspectionId,
                Weight = result.Weight,
                StandardError = result.StandardError,
                ZScore = result.ZScore,
                PiValue = result.PiValue,
                RightSources = result.RightSources,
                LeftSources = result.LeftSources,
                Populations = result.QpadmResultPopulations.Select(qrp => new PopulationResponse
                {
                    Id = qrp.Population.Id,
                    Name = qrp.Population.Name,
                    EraId = qrp.Population.EraId,
                    EraName = qrp.Population.Era.Name,
                    Percentage = qrp.Percentage
                }).ToList()
            };
        }

        public async Task<SubmitVahaduoResultContract.Response?> SubmitVahaduoResultAsync(int inspectionId,
            SubmitVahaduoResultContract.Request request)
        {
            var inspection = await dbContext.GeneticInspections
                .Include(gi => gi.Order)
                .Include(gi => gi.User)
                .Include(gi => gi.VahaduoResult)
                    .ThenInclude(vr => vr!.VahaduoResultPopulations)
                .FirstOrDefaultAsync(gi => gi.Id == inspectionId);

            if (inspection is null)
            {
                return null;
            }

            var populationIds = request.Populations.Select(p => p.PopulationId).ToList();
            var populations = await dbContext.Populations
                .Include(p => p.Era)
                .Where(p => populationIds.Contains(p.Id))
                .ToListAsync();

            var distanceLookup = request.Populations.ToDictionary(p => p.PopulationId, p => p.Distance);

            var joinEntities = populations.Select(p => new VahaduoResultPopulation
            {
                PopulationId = p.Id,
                Distance = distanceLookup.GetValueOrDefault(p.Id)
            }).ToList();

            if (inspection.VahaduoResult is not null)
            {
                inspection.VahaduoResult.UpdatedAt = DateTime.UtcNow;
                inspection.VahaduoResult.VahaduoResultPopulations.Clear();
                foreach (var je in joinEntities)
                {
                    je.VahaduoResultId = inspection.VahaduoResult.Id;
                    inspection.VahaduoResult.VahaduoResultPopulations.Add(je);
                }
            }
            else
            {
                inspection.VahaduoResult = new VahaduoResult
                {
                    GeneticInspectionId = inspectionId,
                    VahaduoResultPopulations = joinEntities,
                    CreatedBy = string.Empty
                };
            }

            inspection.Order.Status = Enum.TryParse<OrderStatus>(request.OrderStatus, out var vahaduoStatus)
                ? vahaduoStatus
                : OrderStatus.InProcess;

            await dbContext.SaveChangesAsync();

            if (inspection.Order.Status == OrderStatus.Completed)
            {
                await notificationService.CreateAndSendAsync(
                    inspection.UserId,
                    NotificationType.OrderCompleted,
                    "Order Completed",
                    $"Your {inspection.Order.Service} analysis results are ready.",
                    inspection.Order.Id.ToString());
            }

            return new SubmitVahaduoResultContract.Response
            {
                Id = inspection.VahaduoResult.Id,
                GeneticInspectionId = inspectionId,
                Populations = populations
                    .OrderBy(p => distanceLookup.GetValueOrDefault(p.Id))
                    .Select(p => new VahaduoPopulationResponse
                    {
                        Id = p.Id,
                        Name = p.Name,
                        EraId = p.EraId,
                        EraName = p.Era.Name,
                        Distance = distanceLookup.GetValueOrDefault(p.Id)
                    }).ToList()
            };
        }

        public async Task<SubmitVahaduoResultContract.Response?> GetVahaduoResultAsync(int inspectionId)
        {
            var result = await dbContext.VahaduoResults
                .AsNoTracking()
                .Include(vr => vr.VahaduoResultPopulations)
                    .ThenInclude(vrp => vrp.Population)
                        .ThenInclude(p => p.Era)
                .FirstOrDefaultAsync(vr => vr.GeneticInspectionId == inspectionId);

            if (result is null)
            {
                return null;
            }

            return new SubmitVahaduoResultContract.Response
            {
                Id = result.Id,
                GeneticInspectionId = inspectionId,
                Populations = result.VahaduoResultPopulations
                    .OrderBy(vrp => vrp.Distance)
                    .Select(vrp => new VahaduoPopulationResponse
                    {
                        Id = vrp.Population.Id,
                        Name = vrp.Population.Name,
                        EraId = vrp.Population.EraId,
                        EraName = vrp.Population.Era.Name,
                        Distance = vrp.Distance
                    }).ToList()
            };
        }
    }
}
