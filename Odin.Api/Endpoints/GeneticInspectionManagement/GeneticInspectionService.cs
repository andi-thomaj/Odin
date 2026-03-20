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
        Task<CreateGeneticInspectionContract.Response> CreateAsync(CreateGeneticInspectionContract.Request request,
            string identityId);
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
            CreateGeneticInspectionContract.Request request,
            string identityId)
        {
            var user = await dbContext.Users.FirstOrDefaultAsync(u => u.IdentityId == identityId)
                ?? throw new InvalidOperationException("Authenticated user not found in the database.");

            var utc = DateTime.UtcNow;
            var order = new Order
            {
                Price = 0,
                Service = OrderService.QPADM,
                Status = OrderStatus.Pending,
                HasViewedResults = false,
                CreatedAt = utc,
                CreatedBy = identityId,
                UpdatedAt = utc,
                UpdatedBy = identityId
            };
            dbContext.Orders.Add(order);
            await dbContext.SaveChangesAsync();

            var geneticInspection = new GeneticInspection
            {
                UserId = user.Id,
                OrderId = order.Id,
                FirstName = request.FirstName,
                MiddleName = request.MiddleName ?? string.Empty,
                LastName = request.LastName,
                RawGeneticFileId = request.RawGeneticFileId,
                CreatedAt = utc,
                CreatedBy = identityId,
                UpdatedAt = utc,
                UpdatedBy = identityId
            };

            var regions = await dbContext.Regions
                .Include(r => r.Ethnicity)
                .Where(r => request.RegionIds.Contains(r.Id))
                .ToListAsync();

            dbContext.GeneticInspections.Add(geneticInspection);
            await dbContext.SaveChangesAsync();

            // Add region associations in bulk
            var regionAssociations = regions.Select(region => new GeneticInspectionRegion
            {
                GeneticInspectionId = geneticInspection.Id,
                GeneticInspection = geneticInspection,
                RegionId = region.Id,
                Region = region
            }).ToList();

            dbContext.GeneticInspectionRegions.AddRange(regionAssociations);
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
                .AsSplitQuery()
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
                .AsSplitQuery()
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

            const long maxFileSize = 50 * 1024 * 1024; // 50 MB
            if (request.File.Length > maxFileSize)
                throw new InvalidOperationException("Genetic file size must not exceed 50 MB.");

            using var memoryStream = new MemoryStream();
            await request.File.CopyToAsync(memoryStream);

            var rawGeneticFile = new RawGeneticFile
            {
                RawDataFileName = request.File.FileName, RawData = memoryStream.ToArray(), CreatedBy = string.Empty
            };

            dbContext.RawGeneticFiles.Add(rawGeneticFile);
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
                .AsSplitQuery()
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

            var eraGroupEntities = request.EraGroups.Select((g, i) =>
            {
                var eraGroup = new QpadmResultEraGroup
                {
                    EraId = g.EraId,
                    PiValue = g.PiValue,
                    RightSources = g.RightSources,
                    LeftSources = g.LeftSources,
                };
                foreach (var p in g.Populations)
                {
                    eraGroup.QpadmResultPopulations.Add(new QpadmResultPopulation
                    {
                        PopulationId = p.PopulationId,
                        Percentage = p.Percentage,
                        StandardError = p.StandardError,
                        ZScore = p.ZScore
                    });
                }
                return eraGroup;
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
                    Populations = eg.QpadmResultPopulations
                        .OrderByDescending(qrp => qrp.Percentage)
                        .ThenBy(qrp => qrp.PopulationId)
                        .Select(qrp => new PopulationResponse
                        {
                            Id = qrp.PopulationId,
                            Name = popById.GetValueOrDefault(qrp.PopulationId)?.Name ?? string.Empty,
                            Percentage = qrp.Percentage,
                            StandardError = qrp.StandardError,
                            ZScore = qrp.ZScore,
                            GeoJson = popById.GetValueOrDefault(qrp.PopulationId)?.GeoJson
                        }).ToList()
                }).ToList()
            };
        }

        public async Task<SubmitQpadmResultContract.Response?> GetQpadmResultAsync(int inspectionId)
        {
            var result = await dbContext.QpadmResults
                .AsNoTracking()
                .AsSplitQuery()
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
                    Populations = eg.QpadmResultPopulations
                        .OrderByDescending(qrp => qrp.Percentage)
                        .ThenBy(qrp => qrp.PopulationId)
                        .Select(qrp => new PopulationResponse
                        {
                            Id = qrp.Population.Id,
                            Name = qrp.Population.Name,
                            Percentage = qrp.Percentage,
                            StandardError = qrp.StandardError,
                            ZScore = qrp.ZScore,
                            GeoJson = qrp.Population.GeoJson
                        }).ToList()
                }).ToList()
            };
        }

    }
}
