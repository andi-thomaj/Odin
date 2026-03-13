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
                RawGeneticFileName = inspection.RawGeneticFile.RawDataFileName,
                OrderStatus = inspection.Order.Status.ToString(),
                CreatedAt = inspection.Order.CreatedAt,
                Country = inspection.User?.Country,
                CountryCode = inspection.User?.CountryCode,
                HasQpadmResult = inspection.QpadmResult != null,
                PaternalHaplogroup = inspection.PaternalHaplogroup,
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
                    RawGeneticFileName = inspection.RawGeneticFile.RawDataFileName,
                    OrderStatus = inspection.Order.Status.ToString(),
                    CreatedAt = inspection.Order.CreatedAt,
                    Country = inspection.User.Country,
                    CountryCode = inspection.User.CountryCode,
                    HasQpadmResult = inspection.QpadmResult != null,
                    PaternalHaplogroup = inspection.PaternalHaplogroup,
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
                RawDataFileName = request.File.FileName, RawData = memoryStream.ToArray(), CreatedBy = string.Empty
            };

            dbContext.RawGeneticFiles.Add(rawGeneticFile);
            await dbContext.SaveChangesAsync();

            inspection.RawGeneticFileId = rawGeneticFile.Id;
            await dbContext.SaveChangesAsync();

            return new UploadGeneticFileContract.Response
            {
                Id = rawGeneticFile.Id,
                FileName = rawGeneticFile.RawDataFileName,
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

            return (inspection.RawGeneticFile.RawData, inspection.RawGeneticFile.RawDataFileName);
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
                .Include(gi => gi.RawGeneticFile)
                .Include(gi => gi.QpadmResult)
                    .ThenInclude(qr => qr!.QpadmResultEraGroups)
                    .ThenInclude(eg => eg.QpadmResultPopulations)
                .FirstOrDefaultAsync(gi => gi.Id == inspectionId);

            if (inspection is null)
            {
                return null;
            }

            inspection.PaternalHaplogroup = request.PaternalHaplogroup;

            if (request.MergedRawDataFile is { Length: > 0 } mergedFile)
            {
                using var ms = new MemoryStream();
                await mergedFile.CopyToAsync(ms);
                inspection.RawGeneticFile!.MergedRawData = ms.ToArray();
                inspection.RawGeneticFile.MergedRawDataFileName = mergedFile.FileName;
            }

            var allPopulationIds = request.EraGroups
                .SelectMany(g => g.Populations.Select(p => p.PopulationId))
                .Distinct()
                .ToList();

            var populations = await dbContext.Populations
                .Include(p => p.Era)
                .Where(p => allPopulationIds.Contains(p.Id))
                .ToListAsync();

            var popById = populations.ToDictionary(p => p.Id);

            var isFirstSubmission = inspection.QpadmResult is null;

            var eraGroupEntities = request.EraGroups.Select(g => new QpadmResultEraGroup
            {
                EraId = g.EraId,
                PiValue = g.PiValue,
                RightSources = g.RightSources,
                LeftSources = g.LeftSources,
            }).ToList();

            if (inspection.QpadmResult is not null)
            {
                inspection.QpadmResult.UpdatedAt = DateTime.UtcNow;

                foreach (var existing in inspection.QpadmResult.QpadmResultEraGroups.ToList())
                    dbContext.RemoveRange(existing.QpadmResultPopulations);
                dbContext.RemoveRange(inspection.QpadmResult.QpadmResultEraGroups);
                inspection.QpadmResult.QpadmResultEraGroups.Clear();
                await dbContext.SaveChangesAsync();

                foreach (var eg in eraGroupEntities)
                    inspection.QpadmResult.QpadmResultEraGroups.Add(eg);
            }
            else
            {
                inspection.QpadmResult = new QpadmResult
                {
                    GeneticInspectionId = inspectionId,
                    QpadmResultEraGroups = eraGroupEntities,
                    CreatedBy = string.Empty
                };
            }

            inspection.Order.Status = Enum.TryParse<OrderStatus>(request.OrderStatus, out var qpadmStatus)
                ? qpadmStatus
                : OrderStatus.InProcess;

            await dbContext.SaveChangesAsync();

            for (var i = 0; i < eraGroupEntities.Count; i++)
            {
                var reqGroup = request.EraGroups[i];
                var entity = eraGroupEntities[i];

                foreach (var p in reqGroup.Populations)
                {
                    entity.QpadmResultPopulations.Add(new QpadmResultPopulation
                    {
                        QpadmResultEraGroupId = entity.Id,
                        PopulationId = p.PopulationId,
                        Percentage = p.Percentage,
                        StandardError = p.StandardError,
                        ZScore = p.ZScore
                    });
                }
            }

            await dbContext.SaveChangesAsync();

            if (isFirstSubmission && inspection.Order.Status == OrderStatus.Completed)
            {
                await notificationService.CreateAndSendAsync(
                    inspection.UserId,
                    NotificationType.OrderCompleted,
                    "Order Completed",
                    $"Your {inspection.Order.Service} analysis results are ready.",
                    inspection.Order.Id.ToString());
            }

            var eras = await dbContext.Eras
                .AsNoTracking()
                .Where(e => request.EraGroups.Select(g => g.EraId).Contains(e.Id))
                .ToDictionaryAsync(e => e.Id);

            return new SubmitQpadmResultContract.Response
            {
                Id = inspection.QpadmResult.Id,
                GeneticInspectionId = inspectionId,
                EraGroups = eraGroupEntities.Select(eg => new EraGroupResponse
                {
                    EraId = eg.EraId,
                    EraName = eras.GetValueOrDefault(eg.EraId)?.Name ?? string.Empty,
                    PiValue = eg.PiValue,
                    RightSources = eg.RightSources,
                    LeftSources = eg.LeftSources,
                    Populations = eg.QpadmResultPopulations.Select(qrp => new PopulationResponse
                    {
                        Id = qrp.PopulationId,
                        Name = popById.GetValueOrDefault(qrp.PopulationId)?.Name ?? string.Empty,
                        Percentage = qrp.Percentage,
                        StandardError = qrp.StandardError,
                        ZScore = qrp.ZScore
                    }).ToList()
                }).ToList()
            };
        }

        public async Task<SubmitQpadmResultContract.Response?> GetQpadmResultAsync(int inspectionId)
        {
            var result = await dbContext.QpadmResults
                .AsNoTracking()
                .Include(qr => qr.QpadmResultEraGroups)
                    .ThenInclude(eg => eg.Era)
                .Include(qr => qr.QpadmResultEraGroups)
                    .ThenInclude(eg => eg.QpadmResultPopulations)
                    .ThenInclude(qrp => qrp.Population)
                .FirstOrDefaultAsync(qr => qr.GeneticInspectionId == inspectionId);

            if (result is null)
            {
                return null;
            }

            return new SubmitQpadmResultContract.Response
            {
                Id = result.Id,
                GeneticInspectionId = inspectionId,
                EraGroups = result.QpadmResultEraGroups.Select(eg => new EraGroupResponse
                {
                    EraId = eg.EraId,
                    EraName = eg.Era.Name,
                    PiValue = eg.PiValue,
                    RightSources = eg.RightSources,
                    LeftSources = eg.LeftSources,
                    Populations = eg.QpadmResultPopulations.Select(qrp => new PopulationResponse
                    {
                        Id = qrp.Population.Id,
                        Name = qrp.Population.Name,
                        Percentage = qrp.Percentage,
                        StandardError = qrp.StandardError,
                        ZScore = qrp.ZScore
                    }).ToList()
                }).ToList()
            };
        }

    }
}
