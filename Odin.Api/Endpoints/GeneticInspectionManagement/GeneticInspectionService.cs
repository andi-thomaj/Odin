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
                .Include(gi => gi.Order)
                .Include(gi => gi.User)
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
                OrderStatus = inspection.Order.Status.ToString(),
                CreatedAt = inspection.Order.CreatedAt,
                Country = inspection.User?.Country,
                CountryCode = inspection.User?.CountryCode,
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
                .Include(gi => gi.Order)
                .Include(gi => gi.User)
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
                    OrderStatus = inspection.Order.Status.ToString(),
                    CreatedAt = inspection.Order.CreatedAt,
                    Country = inspection.User.Country,
                    CountryCode = inspection.User.CountryCode,
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

            var popLookup = request.Populations.ToDictionary(p => p.PopulationId);

            var perEraTotal = populations
                .GroupBy(p => p.EraId)
                .Where(g => g.Sum(p => popLookup.GetValueOrDefault(p.Id)?.Percentage ?? 0) > 100);

            if (perEraTotal.Any())
            {
                return null;
            }

            var joinEntities = populations.Select(p =>
            {
                var item = popLookup.GetValueOrDefault(p.Id);
                return new QpadmResultPopulation
                {
                    PopulationId = p.Id,
                    Percentage = item?.Percentage ?? 0,
                    StandardError = item?.StandardError ?? 0,
                    ZScore = item?.ZScore ?? 0
                };
            }).ToList();

            if (inspection.QpadmResult is not null)
            {
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
                PiValue = inspection.QpadmResult.PiValue,
                RightSources = inspection.QpadmResult.RightSources,
                LeftSources = inspection.QpadmResult.LeftSources,
                Populations = populations.Select(p =>
                {
                    var item = popLookup.GetValueOrDefault(p.Id);
                    return new PopulationResponse
                    {
                        Id = p.Id,
                        Name = p.Name,
                        EraId = p.EraId,
                        EraName = p.Era.Name,
                        Percentage = item?.Percentage ?? 0,
                        StandardError = item?.StandardError ?? 0,
                        ZScore = item?.ZScore ?? 0
                    };
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
                PiValue = result.PiValue,
                RightSources = result.RightSources,
                LeftSources = result.LeftSources,
                Populations = result.QpadmResultPopulations.Select(qrp => new PopulationResponse
                {
                    Id = qrp.Population.Id,
                    Name = qrp.Population.Name,
                    EraId = qrp.Population.EraId,
                    EraName = qrp.Population.Era.Name,
                    Percentage = qrp.Percentage,
                    StandardError = qrp.StandardError,
                    ZScore = qrp.ZScore
                }).ToList()
            };
        }

    }
}
