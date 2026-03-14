using Microsoft.EntityFrameworkCore;
using Odin.Api.Data;
using Odin.Api.Data.Entities;
using Odin.Api.Data.Enums;
using Odin.Api.Endpoints.OrderManagement.Models;
using Odin.Api.Services;

namespace Odin.Api.Endpoints.OrderManagement
{
    public interface IOrderService
    {
        Task<CreateOrderContract.Response> CreateAsync(CreateOrderContract.Request request, string identityId, string? ipAddress = null);
        Task<GetOrderContract.Response?> GetByIdAsync(int id);
        Task<IEnumerable<GetOrderContract.Response>> GetAllAsync();
        Task<GetOrderContract.Response?> UpdateAsync(int id, UpdateOrderContract.Request request);
        Task<bool> DeleteAsync(int id);
        Task<(GetOrderQpadmResultContract.Response? Result, int StatusCode, string? Error)> GetQpadmResultForOrderAsync(int orderId, string identityId);
        Task<(byte[]? FileBytes, string? FileName, int StatusCode, string? Error)> DownloadMergedDataForOrderAsync(int orderId, string identityId);
        Task<(byte[]? FileBytes, string? FileName)?> GetProfilePictureAsync(int orderId);
    }

    public class OrderService(ApplicationDbContext dbContext, IGeoLocationService geoLocationService) : IOrderService
    {
        private const decimal QpadmPrice = 49.99m;
        private const int MaxEthnicities = 4;
        private const int MaxRegionsPerEthnicity = 4;

        public async Task<CreateOrderContract.Response> CreateAsync(CreateOrderContract.Request request, string identityId, string? ipAddress = null)
        {
            var user = await dbContext.Users
                .FirstOrDefaultAsync(u => u.IdentityId == identityId)
                ?? throw new InvalidOperationException("Authenticated user not found in the database.");

            if ((user.Country is null || user.CountryCode is null) && ipAddress is not null)
            {
                var geo = await geoLocationService.GetCountryFromIpAsync(ipAddress);
                user.Country = geo?.Country;
                user.CountryCode = geo?.CountryCode;
            }

            var regions = await dbContext.Regions
                .Include(r => r.Ethnicity)
                .Where(r => request.RegionIds.Contains(r.Id))
                .ToListAsync();

            if (regions.Count == 0)
                throw new InvalidOperationException("None of the provided region IDs are valid.");

            var grouped = regions.GroupBy(r => r.EthnicityId).ToList();

            if (grouped.Count > MaxEthnicities)
                throw new InvalidOperationException(
                    $"You can select at most {MaxEthnicities} countries (ethnicities).");

            var overLimit = grouped.FirstOrDefault(g => g.Count() > MaxRegionsPerEthnicity);
            if (overLimit is not null)
                throw new InvalidOperationException(
                    $"You can select at most {MaxRegionsPerEthnicity} regions for \"{overLimit.First().Ethnicity.Name}\".");

            int rawGeneticFileId;

            if (request.ExistingFileId.HasValue)
            {
                var existingFile = await dbContext.RawGeneticFiles
                    .FirstOrDefaultAsync(f => f.Id == request.ExistingFileId.Value && !f.IsDeleted);

                if (existingFile is null)
                    throw new InvalidOperationException("The selected genetic file does not exist or has been deleted.");

                rawGeneticFileId = existingFile.Id;
            }
            else
            {
                using var memoryStream = new MemoryStream();
                await request.File!.CopyToAsync(memoryStream);

                var rawGeneticFile = new RawGeneticFile
                {
                    RawDataFileName = request.File.FileName,
                    RawData = memoryStream.ToArray(),
                    CreatedBy = identityId
                };
                dbContext.RawGeneticFiles.Add(rawGeneticFile);
                await dbContext.SaveChangesAsync();

                rawGeneticFileId = rawGeneticFile.Id;
            }

            var order = new Data.Entities.Order
            {
                Price = QpadmPrice,
                Service = request.Service,
                Status = OrderStatus.Pending,
                CreatedBy = identityId,
                CreatedAt = DateTime.UtcNow
            };
            dbContext.Orders.Add(order);
            await dbContext.SaveChangesAsync();

            var geneticInspection = new GeneticInspection
            {
                FirstName = request.FirstName,
                MiddleName = request.MiddleName ?? string.Empty,
                LastName = request.LastName,
                Gender = Enum.Parse<Data.Enums.Gender>(request.Gender),
                G25Coordinates = request.G25Coordinates,
                RawGeneticFileId = rawGeneticFileId,
                UserId = user.Id,
                OrderId = order.Id,
                CreatedBy = identityId
            };

            if (request.ProfilePicture is not null && request.ProfilePicture.Length > 0)
            {
                using var picStream = new MemoryStream();
                await request.ProfilePicture.CopyToAsync(picStream);
                geneticInspection.ProfilePicture = picStream.ToArray();
                geneticInspection.ProfilePictureFileName = request.ProfilePicture.FileName;
            }

            dbContext.GeneticInspections.Add(geneticInspection);
            await dbContext.SaveChangesAsync();

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

            return new CreateOrderContract.Response
            {
                Id = order.Id,
                Price = order.Price,
                Service = order.Service.ToString(),
                Status = order.Status.ToString(),
                GeneticInspectionId = geneticInspection.Id
            };
        }

        public async Task<GetOrderContract.Response?> GetByIdAsync(int id)
        {
            var order = await dbContext.Orders
                .AsNoTracking()
                .Include(o => o.GeneticInspection)
                    .ThenInclude(gi => gi.GeneticInspectionRegions)
                .FirstOrDefaultAsync(o => o.Id == id);

            if (order is null)
            {
                return null;
            }

            return new GetOrderContract.Response
            {
                Id = order.Id,
                Price = order.Price,
                Service = order.Service.ToString(),
                Status = order.Status.ToString(),
                GeneticInspectionId = order.GeneticInspection?.Id ?? 0,
                FirstName = order.GeneticInspection?.FirstName ?? string.Empty,
                MiddleName = order.GeneticInspection?.MiddleName ?? string.Empty,
                LastName = order.GeneticInspection?.LastName ?? string.Empty,
                HasProfilePicture = order.GeneticInspection?.ProfilePicture is { Length: > 0 },
                RegionIds = order.GeneticInspection?.GeneticInspectionRegions
                    .Select(gir => gir.RegionId).ToList() ?? [],
                CreatedAt = order.CreatedAt,
                CreatedBy = order.CreatedBy,
                UpdatedAt = order.UpdatedAt,
                UpdatedBy = order.UpdatedBy
            };
        }

        public async Task<IEnumerable<GetOrderContract.Response>> GetAllAsync()
        {
            return await dbContext.Orders
                .AsNoTracking()
                .Include(o => o.GeneticInspection)
                    .ThenInclude(gi => gi.GeneticInspectionRegions)
                .Select(order => new GetOrderContract.Response
                {
                    Id = order.Id,
                    Price = order.Price,
                    Service = order.Service.ToString(),
                    Status = order.Status.ToString(),
                    GeneticInspectionId = order.GeneticInspection != null ? order.GeneticInspection.Id : 0,
                    FirstName = order.GeneticInspection != null ? order.GeneticInspection.FirstName : string.Empty,
                    MiddleName = order.GeneticInspection != null ? order.GeneticInspection.MiddleName : string.Empty,
                    LastName = order.GeneticInspection != null ? order.GeneticInspection.LastName : string.Empty,
                    G25Coordinates = order.GeneticInspection != null ? order.GeneticInspection.G25Coordinates : null,
                    HasProfilePicture = order.GeneticInspection != null && order.GeneticInspection.ProfilePicture != null && order.GeneticInspection.ProfilePicture.Length > 0,
                    RegionIds = order.GeneticInspection != null
                        ? order.GeneticInspection.GeneticInspectionRegions.Select(gir => gir.RegionId).ToList()
                        : new List<int>(),
                    CreatedAt = order.CreatedAt,
                    CreatedBy = order.CreatedBy,
                    UpdatedAt = order.UpdatedAt,
                    UpdatedBy = order.UpdatedBy
                })
                .ToListAsync();
        }

        public async Task<GetOrderContract.Response?> UpdateAsync(int id, UpdateOrderContract.Request request)
        {
            var order = await dbContext.Orders
                .Include(o => o.GeneticInspection)
                    .ThenInclude(gi => gi.GeneticInspectionRegions)
                .FirstOrDefaultAsync(o => o.Id == id);

            if (order is null || order.GeneticInspection is null)
            {
                return null;
            }

            if (order.Status != OrderStatus.Pending)
                throw new InvalidOperationException("Only orders with status 'Pending' can be edited.");

            var regions = await dbContext.Regions
                .Include(r => r.Ethnicity)
                .Where(r => request.RegionIds.Contains(r.Id))
                .ToListAsync();

            if (regions.Count == 0)
                throw new InvalidOperationException("None of the provided region IDs are valid.");

            var grouped = regions.GroupBy(r => r.EthnicityId).ToList();

            if (grouped.Count > MaxEthnicities)
                throw new InvalidOperationException(
                    $"You can select at most {MaxEthnicities} countries (ethnicities).");

            var overLimit = grouped.FirstOrDefault(g => g.Count() > MaxRegionsPerEthnicity);
            if (overLimit is not null)
                throw new InvalidOperationException(
                    $"You can select at most {MaxRegionsPerEthnicity} regions for \"{overLimit.First().Ethnicity.Name}\".");

            order.GeneticInspection.FirstName = request.FirstName;
            order.GeneticInspection.MiddleName = request.MiddleName ?? string.Empty;
            order.GeneticInspection.LastName = request.LastName;
            order.GeneticInspection.G25Coordinates = request.G25Coordinates;
            order.UpdatedAt = DateTime.UtcNow;

            if (request.ProfilePicture is not null && request.ProfilePicture.Length > 0)
            {
                using var picStream = new MemoryStream();
                await request.ProfilePicture.CopyToAsync(picStream);
                order.GeneticInspection.ProfilePicture = picStream.ToArray();
                order.GeneticInspection.ProfilePictureFileName = request.ProfilePicture.FileName;
            }

            dbContext.GeneticInspectionRegions.RemoveRange(order.GeneticInspection.GeneticInspectionRegions);

            foreach (var region in regions)
            {
                dbContext.GeneticInspectionRegions.Add(new GeneticInspectionRegion
                {
                    GeneticInspectionId = order.GeneticInspection.Id,
                    GeneticInspection = order.GeneticInspection,
                    RegionId = region.Id,
                    Region = region
                });
            }

            await dbContext.SaveChangesAsync();

            return new GetOrderContract.Response
            {
                Id = order.Id,
                Price = order.Price,
                Service = order.Service.ToString(),
                Status = order.Status.ToString(),
                GeneticInspectionId = order.GeneticInspection.Id,
                FirstName = order.GeneticInspection.FirstName,
                MiddleName = order.GeneticInspection.MiddleName,
                LastName = order.GeneticInspection.LastName,
                G25Coordinates = order.GeneticInspection.G25Coordinates,
                HasProfilePicture = order.GeneticInspection.ProfilePicture is { Length: > 0 },
                RegionIds = regions.Select(r => r.Id).ToList(),
                CreatedAt = order.CreatedAt,
                CreatedBy = order.CreatedBy,
                UpdatedAt = order.UpdatedAt,
                UpdatedBy = order.UpdatedBy
            };
        }

        public async Task<bool> DeleteAsync(int id)
        {
            var order = await dbContext.Orders.FindAsync(id);

            if (order is null)
            {
                return false;
            }

            dbContext.Orders.Remove(order);
            await dbContext.SaveChangesAsync();
            return true;
        }

        public async Task<(GetOrderQpadmResultContract.Response? Result, int StatusCode, string? Error)> GetQpadmResultForOrderAsync(int orderId, string identityId)
        {
            var order = await dbContext.Orders
                .AsNoTracking()
                .Include(o => o.GeneticInspection)
                    .ThenInclude(gi => gi!.RawGeneticFile)
                .FirstOrDefaultAsync(o => o.Id == orderId);

            if (order is null)
                return (null, 404, $"Order with ID {orderId} not found.");

            if (order.CreatedBy != identityId)
                return (null, 403, "You do not have permission to view this order's results.");

            if (order.Status != OrderStatus.Completed)
                return (null, 400, "Results are only available for completed orders.");

            if (order.GeneticInspection is null)
                return (null, 404, "No genetic inspection associated with this order.");

            var qpadmResult = await dbContext.QpadmResults
                .AsNoTracking()
                .Include(qr => qr.QpadmResultEraGroups)
                    .ThenInclude(eg => eg.Era)
                .Include(qr => qr.QpadmResultEraGroups)
                    .ThenInclude(eg => eg.QpadmResultPopulations)
                    .ThenInclude(qrp => qrp.Population)
                .FirstOrDefaultAsync(qr => qr.GeneticInspectionId == order.GeneticInspection.Id);

            if (qpadmResult is null)
                return (null, 404, "No QPADM result found for this order.");

            var response = new GetOrderQpadmResultContract.Response
            {
                FirstName = order.GeneticInspection.FirstName,
                MiddleName = order.GeneticInspection.MiddleName,
                LastName = order.GeneticInspection.LastName,
                PaternalHaplogroup = order.GeneticInspection.PaternalHaplogroup,
                HasMergedRawData = order.GeneticInspection.RawGeneticFile?.MergedRawData is { Length: > 0 },
                EraGroups = qpadmResult.QpadmResultEraGroups.Select(eg => new GetOrderQpadmResultContract.EraGroupResult
                {
                    EraId = eg.EraId,
                    EraName = eg.Era.Name,
                    PiValue = eg.PiValue,
                    RightSources = eg.RightSources,
                    LeftSources = eg.LeftSources,
                    Populations = eg.QpadmResultPopulations.Select(qrp => new GetOrderQpadmResultContract.PopulationResult
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

            return (response, 200, null);
        }

        public async Task<(byte[]? FileBytes, string? FileName, int StatusCode, string? Error)> DownloadMergedDataForOrderAsync(int orderId, string identityId)
        {
            var order = await dbContext.Orders
                .AsNoTracking()
                .Include(o => o.GeneticInspection)
                    .ThenInclude(gi => gi!.RawGeneticFile)
                .FirstOrDefaultAsync(o => o.Id == orderId);

            if (order is null)
                return (null, null, 404, $"Order with ID {orderId} not found.");

            if (order.CreatedBy != identityId)
                return (null, null, 403, "You do not have permission to access this order's data.");

            if (order.GeneticInspection?.RawGeneticFile?.MergedRawData is not { Length: > 0 } mergedData)
                return (null, null, 404, "No merged data available for this order.");

            var fileName = order.GeneticInspection.RawGeneticFile.MergedRawDataFileName ?? "merged-data";

            return (mergedData, fileName, 200, null);
        }

        public async Task<(byte[]? FileBytes, string? FileName)?> GetProfilePictureAsync(int orderId)
        {
            var order = await dbContext.Orders
                .AsNoTracking()
                .Include(o => o.GeneticInspection)
                .FirstOrDefaultAsync(o => o.Id == orderId);

            if (order?.GeneticInspection?.ProfilePicture is not { Length: > 0 } pictureData)
                return null;

            return (pictureData, order.GeneticInspection.ProfilePictureFileName ?? "profile-picture");
        }

    }
}
