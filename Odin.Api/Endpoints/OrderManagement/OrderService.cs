using System.Diagnostics;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Odin.Api.Data;
using Odin.Api.Data.Entities;
using Odin.Api.Data.Enums;
using Odin.Api.Endpoints.Admin.Models;
using Odin.Api.Endpoints.G25Calculations;
using Odin.Api.Endpoints.G25Calculations.Models;
using Odin.Api.Endpoints.OrderManagement.Models;
using Odin.Api.Services;

namespace Odin.Api.Endpoints.OrderManagement;

public interface IOrderService
{
    Task<CreateOrderContract.Response> CreateAsync(CreateOrderContract.Request request, string identityId, string? ipAddress = null);
    Task<GetOrderContract.Response?> GetByIdAsync(int id, string identityId);
    Task<IEnumerable<GetOrderContract.Response>> GetAllAsync(string identityId);
    Task<(GetOrderContract.Response? Response, int StatusCode)> UpdateAsync(int id, string identityId, UpdateOrderContract.Request request);
    Task<bool> DeleteAsync(int id);
    Task<(GetOrderQpadmResultContract.Response? Result, int StatusCode, string? Error)> GetQpadmResultForOrderAsync(int orderId, string identityId);
    Task<(GetOrderG25ResultContract.Response? Result, int StatusCode, string? Error)> GetG25ResultForOrderAsync(int orderId, string identityId);
    Task<(byte[]? FileBytes, string? FileName, int StatusCode, string? Error)> DownloadMergedDataForOrderAsync(int orderId, string identityId);
    Task<(byte[]? FileBytes, string? FileName, int StatusCode, string? Error)> GetProfilePictureAsync(int orderId, string identityId);
    Task<(bool Success, int StatusCode, string? Error)> MarkQpadmResultsAsViewedAsync(int orderId, string identityId);
    Task<(bool Success, int StatusCode, string? Error)> MarkG25ResultsAsViewedAsync(int orderId, string identityId);
    Task<RecomputeG25DistancesContract.Response> RecomputeG25DistanceResultsAsync(string identityId, IReadOnlyList<int>? inspectionIds = null);
    Task<List<AdminG25InspectionContract.ListItem>> GetAdminG25InspectionsAsync();
}

public class OrderService(
    ApplicationDbContext dbContext,
    IGeoLocationService geoLocationService,
    IOrderPricingService orderPricingService,
    IG25CalculationService g25CalculationService,
    ILogger<OrderService> logger) : IOrderService
{
        private const int MaxEthnicities = 4;
        private const int MaxRegionsPerEthnicity = 4;
        private const string G25DistanceResultsVersion = "v1";
        private const int G25DistanceMaxResults = 25;

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

            if (request.Service == ServiceType.g25)
                return await CreateG25OrderAsync(request, identityId, user);

            var regions = await dbContext.QpadmRegions
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

                if (existingFile.CreatedBy != identityId)
                    throw new InvalidOperationException("The selected genetic file does not belong to your account.");

                rawGeneticFileId = existingFile.Id;
            }
            else
            {
                const long maxFileSize = 50 * 1024 * 1024; // 50 MB
                if (request.File!.Length > maxFileSize)
                    throw new InvalidOperationException("Genetic file size must not exceed 50 MB.");

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

            Data.Entities.PaddlePayment? paddlePayment = null;
            if (request.PaddlePaymentId.HasValue)
            {
                paddlePayment = await dbContext.PaddlePayments
                    .FirstOrDefaultAsync(p => p.Id == request.PaddlePaymentId.Value);

                if (paddlePayment is null)
                    throw new InvalidOperationException("The specified payment was not found.");
                if (paddlePayment.UserId != identityId)
                    throw new InvalidOperationException("The specified payment does not belong to your account.");
                if (paddlePayment.Status != "paid")
                    throw new InvalidOperationException("The specified payment is not in a valid state.");
                if (paddlePayment.OrderId is not null)
                    throw new InvalidOperationException("The specified payment has already been used for an order.");
            }

            var pricing = await orderPricingService.ComputeAsync(
                request.Service,
                request.AddonIds,
                request.PromoCode);

            var now = DateTime.UtcNow;
            var order = new QpadmOrder
            {
                Price = pricing.Total,
                Status = OrderStatus.Pending,
                CreatedBy = identityId,
                CreatedAt = now,
                UpdatedAt = now,
                UpdatedBy = identityId,
                DiscountAmount = pricing.DiscountAmount,
                PromoCodeId = pricing.PromoCodeId,
                ExpeditedProcessing = pricing.ExpeditedProcessing,
                IncludesYHaplogroup = pricing.IncludesYHaplogroup,
                IncludesRawMerge = pricing.IncludesRawMerge
            };
            dbContext.QpadmOrders.Add(order);
            await dbContext.SaveChangesAsync();

            if (paddlePayment is not null)
            {
                paddlePayment.OrderId = order.Id;
                paddlePayment.UpdatedAt = now;
            }

            foreach (var line in pricing.AddonLines)
            {
                dbContext.OrderLineAddons.Add(new OrderLineAddon
                {
                    OrderId = order.Id,
                    ProductAddonId = line.ProductAddonId,
                    UnitPriceSnapshot = line.UnitPrice
                });
            }

            var geneticInspection = new QpadmGeneticInspection
            {
                FirstName = request.FirstName,
                MiddleName = request.MiddleName ?? string.Empty,
                LastName = request.LastName,
                Gender = Enum.Parse<Data.Enums.Gender>(request.Gender),
                RawGeneticFileId = rawGeneticFileId,
                UserId = user.Id,
                OrderId = order.Id,
                CreatedBy = identityId
            };

            if (request.ProfilePicture is not null && request.ProfilePicture.Length > 0)
            {
                const long maxPictureSize = 10 * 1024 * 1024; // 10 MB
                if (request.ProfilePicture.Length > maxPictureSize)
                    throw new InvalidOperationException("Profile picture size must not exceed 10 MB.");

                using var picStream = new MemoryStream();
                await request.ProfilePicture.CopyToAsync(picStream);
                geneticInspection.ProfilePicture = picStream.ToArray();
                geneticInspection.ProfilePictureFileName = request.ProfilePicture.FileName;
            }

            dbContext.QpadmGeneticInspections.Add(geneticInspection);
            await dbContext.SaveChangesAsync();

            var regionAssociations = regions.Select(region => new QpadmGeneticInspectionRegion
            {
                GeneticInspectionId = geneticInspection.Id,
                GeneticInspection = geneticInspection,
                RegionId = region.Id,
                Region = region
            }).ToList();

            dbContext.QpadmGeneticInspectionRegions.AddRange(regionAssociations);

            if (pricing.PromoCodeId is { } promoId)
            {
                var promo = await dbContext.PromoCodes.FirstAsync(p => p.Id == promoId);
                promo.RedemptionCount++;
            }

            await dbContext.SaveChangesAsync();

            return new CreateOrderContract.Response
            {
                Id = order.Id,
                Price = order.Price,
                Service = ServiceType.qpAdm.ToString(),
                Status = order.Status.ToString(),
                GeneticInspectionId = geneticInspection.Id
            };
        }

        private async Task<CreateOrderContract.Response> CreateG25OrderAsync(
            CreateOrderContract.Request request,
            string identityId,
            User user)
        {
            if (request.PaddlePaymentId.HasValue)
                throw new InvalidOperationException("Paddle payment is not yet supported for G25 orders.");

            int rawGeneticFileId;
            string? g25Coordinates = null;

            if (request.ExistingFileId.HasValue)
            {
                var existingFile = await dbContext.RawGeneticFiles
                    .FirstOrDefaultAsync(f => f.Id == request.ExistingFileId.Value && !f.IsDeleted);

                if (existingFile is null)
                    throw new InvalidOperationException("The selected genetic file does not exist or has been deleted.");

                if (existingFile.CreatedBy != identityId)
                    throw new InvalidOperationException("The selected genetic file does not belong to your account.");

                rawGeneticFileId = existingFile.Id;
            }
            else if (request.File is not null && request.File.Length > 0)
            {
                const long maxFileSize = 50 * 1024 * 1024; // 50 MB
                if (request.File.Length > maxFileSize)
                    throw new InvalidOperationException("Genetic file size must not exceed 50 MB.");

                using var memoryStream = new MemoryStream();
                await request.File.CopyToAsync(memoryStream);

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
            else
            {
                var coordinates = request.G25Coordinates?.Trim();

                if (string.IsNullOrWhiteSpace(coordinates))
                    throw new InvalidOperationException("G25 coordinates are required when no genetic file is provided.");

                var sanitizedFirst = string.Concat((request.FirstName ?? "user").Where(char.IsLetterOrDigit));
                var sanitizedLast = string.Concat((request.LastName ?? "").Where(char.IsLetterOrDigit));
                var coordinatesFileName = $"g25-coordinates-{sanitizedFirst}-{sanitizedLast}-{DateTime.UtcNow:yyyyMMddHHmmssfff}.txt";

                var coordinatesFile = new RawGeneticFile
                {
                    RawDataFileName = coordinatesFileName,
                    RawData = Encoding.UTF8.GetBytes(coordinates),
                    CreatedBy = identityId
                };
                dbContext.RawGeneticFiles.Add(coordinatesFile);
                await dbContext.SaveChangesAsync();

                rawGeneticFileId = coordinatesFile.Id;
                g25Coordinates = coordinates;
            }

            var pricing = await orderPricingService.ComputeAsync(
                request.Service,
                request.AddonIds,
                request.PromoCode);

            var now = DateTime.UtcNow;
            var order = new G25Order
            {
                Price = pricing.Total,
                Status = OrderStatus.Pending,
                CreatedBy = identityId,
                CreatedAt = now,
                UpdatedAt = now,
                UpdatedBy = identityId,
                DiscountAmount = pricing.DiscountAmount,
                ExpeditedProcessing = pricing.ExpeditedProcessing
            };
            dbContext.G25Orders.Add(order);
            await dbContext.SaveChangesAsync();

            var geneticInspection = new G25GeneticInspection
            {
                FirstName = request.FirstName,
                MiddleName = request.MiddleName ?? string.Empty,
                LastName = request.LastName,
                Gender = Enum.Parse<Data.Enums.Gender>(request.Gender),
                RawGeneticFileId = rawGeneticFileId,
                G25Coordinates = g25Coordinates,
                UserId = user.Id,
                OrderId = order.Id
            };

            if (request.ProfilePicture is not null && request.ProfilePicture.Length > 0)
            {
                const long maxPictureSize = 10 * 1024 * 1024; // 10 MB
                if (request.ProfilePicture.Length > maxPictureSize)
                    throw new InvalidOperationException("Profile picture size must not exceed 10 MB.");

                using var picStream = new MemoryStream();
                await request.ProfilePicture.CopyToAsync(picStream);
                geneticInspection.ProfilePicture = picStream.ToArray();
                geneticInspection.ProfilePictureFileName = request.ProfilePicture.FileName;
            }

            dbContext.G25GeneticInspections.Add(geneticInspection);

            if (pricing.PromoCodeId is { } promoId)
            {
                var promo = await dbContext.PromoCodes.FirstAsync(p => p.Id == promoId);
                promo.RedemptionCount++;
            }

            await dbContext.SaveChangesAsync();

            if (!string.IsNullOrWhiteSpace(g25Coordinates))
            {
                var persistedEras = await ComputeAndPersistG25DistancesAsync(geneticInspection.Id, g25Coordinates!, request.FirstName, request.LastName, identityId);
                if (persistedEras > 0)
                {
                    order.Status = OrderStatus.Completed;
                    order.UpdatedAt = DateTime.UtcNow;
                    order.UpdatedBy = identityId;
                    await dbContext.SaveChangesAsync();
                }
                else
                {
                    logger.LogWarning(
                        "G25 order {OrderId} (inspection {InspectionId}) produced no distance results; leaving status Pending. Verify that at least one G25Era has an attached G25DistanceFile with matching column count.",
                        order.Id, geneticInspection.Id);
                }
            }

            return new CreateOrderContract.Response
            {
                Id = order.Id,
                Price = order.Price,
                Service = ServiceType.g25.ToString(),
                Status = order.Status.ToString(),
                GeneticInspectionId = geneticInspection.Id
            };
        }

        private async Task<int> ComputeAndPersistG25DistancesAsync(
            int geneticInspectionId,
            string coordinates,
            string firstName,
            string lastName,
            string identityId)
        {
            var eras = await dbContext.G25Eras
                .AsNoTracking()
                .Where(e => e.DistanceFile != null)
                .Select(e => new { e.Id, e.Name })
                .ToListAsync();

            if (eras.Count == 0)
            {
                logger.LogWarning(
                    "G25 distance compute skipped for inspection {InspectionId}: no G25Eras have an attached G25DistanceFile.",
                    geneticInspectionId);
                return 0;
            }

            var targetName = BuildTargetName(firstName, lastName);
            var normalizedTarget = NormalizeCoordinatesForTarget(coordinates, targetName);
            var persisted = 0;

            foreach (var era in eras)
            {
                var (response, error, notFound) = await g25CalculationService.ComputeDistancesAsync(
                    new ComputeDistancesContract.Request
                    {
                        TargetCoordinates = normalizedTarget,
                        G25EraId = era.Id,
                        MaxResults = G25DistanceMaxResults
                    });

                if (response is null)
                {
                    logger.LogWarning(
                        "G25 distance compute failed for inspection {InspectionId}, era {EraId} ({EraName}). NotFound={NotFound}. Error={Error}",
                        geneticInspectionId, era.Id, era.Name, notFound, error);
                    continue;
                }

                if (response.Results.Count == 0)
                {
                    logger.LogWarning(
                        "G25 distance compute for inspection {InspectionId}, era {EraId} ({EraName}) returned no target results.",
                        geneticInspectionId, era.Id, era.Name);
                    continue;
                }

                var firstTarget = response.Results[0];
                var populations = firstTarget.Rows
                    .Select((row, index) => new G25DistancePopulation
                    {
                        Name = row.Name,
                        Distance = row.Distance,
                        Rank = index + 1
                    })
                    .ToList();

                if (populations.Count == 0)
                {
                    logger.LogWarning(
                        "G25 distance compute for inspection {InspectionId}, era {EraId} ({EraName}) produced 0 populations.",
                        geneticInspectionId, era.Id, era.Name);
                    continue;
                }

                dbContext.G25DistanceResults.Add(new G25DistanceResult
                {
                    GeneticInspectionId = geneticInspectionId,
                    G25EraId = era.Id,
                    ResultsVersion = G25DistanceResultsVersion,
                    Populations = populations,
                    CreatedBy = identityId
                });
                persisted++;
                logger.LogInformation(
                    "G25 distance compute for inspection {InspectionId}, era {EraId} ({EraName}) persisted {Count} populations.",
                    geneticInspectionId, era.Id, era.Name, populations.Count);
            }

            if (persisted > 0)
                await dbContext.SaveChangesAsync();

            return persisted;
        }

        private static string BuildTargetName(string firstName, string lastName)
        {
            var sanitizedFirst = string.Concat((firstName ?? "target").Where(char.IsLetterOrDigit));
            var sanitizedLast = string.Concat((lastName ?? "").Where(char.IsLetterOrDigit));
            var combined = string.IsNullOrEmpty(sanitizedLast) ? sanitizedFirst : $"{sanitizedFirst}_{sanitizedLast}";
            return string.IsNullOrEmpty(combined) ? "target" : combined;
        }

        private static string NormalizeCoordinatesForTarget(string coordinates, string targetName)
        {
            var trimmed = coordinates.Trim();
            var firstLine = trimmed.Split('\n', 2)[0].Trim();
            var firstComma = firstLine.IndexOf(',');
            if (firstComma <= 0) return trimmed;

            var leader = firstLine[..firstComma].Trim();
            if (double.TryParse(leader, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out _))
            {
                return $"{targetName},{trimmed}";
            }

            return trimmed;
        }

        public async Task<GetOrderContract.Response?> GetByIdAsync(int id, string identityId)
        {
            var order = await dbContext.QpadmOrders
                .AsNoTracking()
                .Include(o => o.GeneticInspection)
                    .ThenInclude(gi => gi.GeneticInspectionRegions)
                    .ThenInclude(gir => gir.Region)
                .FirstOrDefaultAsync(o => o.Id == id && o.CreatedBy == identityId);

            if (order is null)
            {
                return null;
            }

            return new GetOrderContract.Response
            {
                Id = order.Id,
                Price = order.Price,
                Service = ServiceType.qpAdm.ToString(),
                Status = order.Status.ToString(),
                GeneticInspectionId = order.GeneticInspection?.Id ?? 0,
                FirstName = order.GeneticInspection?.FirstName ?? string.Empty,
                MiddleName = order.GeneticInspection?.MiddleName ?? string.Empty,
                LastName = order.GeneticInspection?.LastName ?? string.Empty,
                Gender = order.GeneticInspection?.Gender?.ToString(),
                HasProfilePicture = order.GeneticInspection?.ProfilePicture is { Length: > 0 },
                HasViewedResults = order.HasViewedResults,
                RegionIds = order.GeneticInspection?.GeneticInspectionRegions
                    .Select(gir => gir.RegionId).ToList() ?? [],
                EthnicityIds = order.GeneticInspection?.GeneticInspectionRegions
                    .Select(gir => gir.Region.EthnicityId).Distinct().OrderBy(id => id).ToList() ?? [],
                CreatedAt = order.CreatedAt,
                CreatedBy = order.CreatedBy,
                UpdatedAt = order.UpdatedAt,
                UpdatedBy = order.UpdatedBy
            };
        }

        public async Task<IEnumerable<GetOrderContract.Response>> GetAllAsync(string identityId)
        {
            var qpadmOrders = await dbContext.QpadmOrders
                .AsNoTracking()
                .Where(o => o.CreatedBy == identityId)
                .Include(o => o.GeneticInspection)
                    .ThenInclude(gi => gi.GeneticInspectionRegions)
                    .ThenInclude(gir => gir.Region)
                .Select(order => new GetOrderContract.Response
                {
                    Id = order.Id,
                    Price = order.Price,
                    Service = "qpAdm",
                    Status = order.Status.ToString(),
                    GeneticInspectionId = order.GeneticInspection != null ? order.GeneticInspection.Id : 0,
                    FirstName = order.GeneticInspection != null ? order.GeneticInspection.FirstName : string.Empty,
                    MiddleName = order.GeneticInspection != null ? order.GeneticInspection.MiddleName : string.Empty,
                    LastName = order.GeneticInspection != null ? order.GeneticInspection.LastName : string.Empty,
                    Gender = order.GeneticInspection != null ? order.GeneticInspection.Gender.ToString() : null,
                    HasProfilePicture = order.GeneticInspection != null && order.GeneticInspection.ProfilePicture != null && order.GeneticInspection.ProfilePicture.Length > 0,
                    HasViewedResults = order.HasViewedResults,
                    RegionIds = order.GeneticInspection != null
                        ? order.GeneticInspection.GeneticInspectionRegions.Select(gir => gir.RegionId).ToList()
                        : new List<int>(),
                    EthnicityIds = order.GeneticInspection != null
                        ? order.GeneticInspection.GeneticInspectionRegions.Select(gir => gir.Region.EthnicityId).Distinct().OrderBy(id => id).ToList()
                        : new List<int>(),
                    CreatedAt = order.CreatedAt,
                    CreatedBy = order.CreatedBy,
                    UpdatedAt = order.UpdatedAt,
                    UpdatedBy = order.UpdatedBy
                })
                .ToListAsync();

            var g25Orders = await dbContext.G25Orders
                .AsNoTracking()
                .Where(o => o.CreatedBy == identityId)
                .Include(o => o.GeneticInspection)
                .Select(order => new GetOrderContract.Response
                {
                    Id = order.Id,
                    Price = order.Price,
                    Service = "g25",
                    Status = order.Status.ToString(),
                    GeneticInspectionId = order.GeneticInspection != null ? order.GeneticInspection.Id : 0,
                    FirstName = order.GeneticInspection != null ? order.GeneticInspection.FirstName : string.Empty,
                    MiddleName = order.GeneticInspection != null ? order.GeneticInspection.MiddleName : string.Empty,
                    LastName = order.GeneticInspection != null ? order.GeneticInspection.LastName : string.Empty,
                    Gender = order.GeneticInspection != null ? order.GeneticInspection.Gender.ToString() : null,
                    HasProfilePicture = order.GeneticInspection != null && order.GeneticInspection.ProfilePicture != null && order.GeneticInspection.ProfilePicture.Length > 0,
                    HasViewedResults = order.HasViewedResults,
                    RegionIds = new List<int>(),
                    EthnicityIds = new List<int>(),
                    CreatedAt = order.CreatedAt,
                    CreatedBy = order.CreatedBy,
                    UpdatedAt = order.UpdatedAt,
                    UpdatedBy = order.UpdatedBy
                })
                .ToListAsync();

            return qpadmOrders.Concat(g25Orders).OrderByDescending(o => o.CreatedAt).ToList();
        }

        public async Task<(GetOrderContract.Response? Response, int StatusCode)> UpdateAsync(int id, string identityId, UpdateOrderContract.Request request)
        {
            var order = await dbContext.QpadmOrders
                .Include(o => o.GeneticInspection)
                    .ThenInclude(gi => gi.GeneticInspectionRegions)
                .FirstOrDefaultAsync(o => o.Id == id);

            if (order is null || order.GeneticInspection is null)
            {
                return (null, 404);
            }

            if (order.CreatedBy != identityId)
            {
                return (null, 403);
            }

            if (order.Status != OrderStatus.Pending)
                throw new InvalidOperationException("Only orders with status 'Pending' can be edited.");

            var regions = await dbContext.QpadmRegions
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
            order.UpdatedAt = DateTime.UtcNow;

            if (request.ProfilePicture is not null && request.ProfilePicture.Length > 0)
            {
                using var picStream = new MemoryStream();
                await request.ProfilePicture.CopyToAsync(picStream);
                order.GeneticInspection.ProfilePicture = picStream.ToArray();
                order.GeneticInspection.ProfilePictureFileName = request.ProfilePicture.FileName;
            }

            dbContext.QpadmGeneticInspectionRegions.RemoveRange(order.GeneticInspection.GeneticInspectionRegions);

            var regionAssociations = regions.Select(region => new QpadmGeneticInspectionRegion
            {
                GeneticInspectionId = order.GeneticInspection.Id,
                GeneticInspection = order.GeneticInspection,
                RegionId = region.Id,
                Region = region
            }).ToList();

            dbContext.QpadmGeneticInspectionRegions.AddRange(regionAssociations);

            await dbContext.SaveChangesAsync();

            return (new GetOrderContract.Response
            {
                Id = order.Id,
                Price = order.Price,
                Service = ServiceType.qpAdm.ToString(),
                Status = order.Status.ToString(),
                GeneticInspectionId = order.GeneticInspection.Id,
                FirstName = order.GeneticInspection.FirstName,
                MiddleName = order.GeneticInspection.MiddleName,
                LastName = order.GeneticInspection.LastName,
                HasProfilePicture = order.GeneticInspection.ProfilePicture is { Length: > 0 },
                HasViewedResults = order.HasViewedResults,
                RegionIds = regions.Select(r => r.Id).ToList(),
                EthnicityIds = regions.Select(r => r.EthnicityId).Distinct().OrderBy(id => id).ToList(),
                Gender = order.GeneticInspection.Gender?.ToString(),
                CreatedAt = order.CreatedAt,
                CreatedBy = order.CreatedBy,
                UpdatedAt = order.UpdatedAt,
                UpdatedBy = order.UpdatedBy
            }, 200);
        }

        public async Task<bool> DeleteAsync(int id)
        {
            var order = await dbContext.QpadmOrders.FindAsync(id);

            if (order is null)
            {
                return false;
            }

            dbContext.QpadmOrders.Remove(order);
            await dbContext.SaveChangesAsync();
            return true;
        }

        public async Task<(GetOrderQpadmResultContract.Response? Result, int StatusCode, string? Error)> GetQpadmResultForOrderAsync(int orderId, string identityId)
        {
            var order = await dbContext.QpadmOrders
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
                .AsSplitQuery()
                .Include(qr => qr.QpadmResultEraGroups)
                    .ThenInclude(eg => eg.Era)
                .Include(qr => qr.QpadmResultEraGroups)
                    .ThenInclude(eg => eg.QpadmResultPopulations)
                    .ThenInclude(qrp => qrp.Population)
                    .ThenInclude(p => p.MusicTrack)
                .FirstOrDefaultAsync(qr => qr.GeneticInspectionId == order.GeneticInspection.Id);

            if (qpadmResult is null)
                return (null, 404, "No QPADM result found for this order.");

            var introTrack = await dbContext.MusicTracks
                .AsNoTracking()
                .Where(t => t.DisplayOrder == 0)
                .Select(t => new { t.Id, HasFile = t.MusicTrackFile != null })
                .FirstOrDefaultAsync();

            // Pre-query which tracks/populations have media files (avoids loading blobs).
            // These are LINQ-to-Entities queries that translate to SQL, unlike the
            // in-memory .Select() below where navigation properties aren't loaded.
            var trackIdsWithAudio = new HashSet<int>(
                await dbContext.MusicTrackFiles.Select(f => f.MusicTrackId).ToListAsync());

            var response = new GetOrderQpadmResultContract.Response
            {
                FirstName = order.GeneticInspection.FirstName,
                MiddleName = order.GeneticInspection.MiddleName,
                LastName = order.GeneticInspection.LastName,
                HasMergedRawData = order.GeneticInspection.RawGeneticFile?.MergedRawData is { Length: > 0 },
                HasProfilePicture = order.GeneticInspection.ProfilePicture is { Length: > 0 },
                Gender = order.GeneticInspection.Gender?.ToString(),
                IntroTrackId = introTrack?.Id,
                HasIntroAudioFile = introTrack?.HasFile ?? false,
                EraGroups = qpadmResult.QpadmResultEraGroups.Select(eg => new GetOrderQpadmResultContract.EraGroupResult
                {
                    EraId = eg.EraId,
                    EraName = eg.Era.Name,
                    PValue = eg.PValue,
                    RightSources = eg.RightSources,
                    Populations = eg.QpadmResultPopulations
                        .OrderByDescending(qrp => qrp.Percentage)
                        .ThenBy(qrp => qrp.PopulationId)
                        .Select(qrp => new GetOrderQpadmResultContract.PopulationResult
                        {
                            Id = qrp.Population.Id,
                            Name = qrp.Population.Name,
                            Description = qrp.Population.Description,
                            GeoJson = qrp.Population.GeoJson,
                            IconFileName = qrp.Population.IconFileName,
                            Color = qrp.Population.Color,
                            MusicTrackId = qrp.Population.MusicTrackId,
                            MusicTrackFileName = qrp.Population.MusicTrack.FileName,
                            HasAudioFile = trackIdsWithAudio.Contains(qrp.Population.MusicTrackId),
                            Percentage = qrp.Percentage,
                            StandardError = qrp.StandardError,
                            ZScore = qrp.ZScore,
                        }).ToList()
                }).ToList()
            };

            return (response, 200, null);
        }

        public async Task<(GetOrderG25ResultContract.Response? Result, int StatusCode, string? Error)> GetG25ResultForOrderAsync(int orderId, string identityId)
        {
            var order = await dbContext.G25Orders
                .AsNoTracking()
                .FirstOrDefaultAsync(o => o.Id == orderId);

            if (order is null)
                return (null, 404, $"Order with ID {orderId} not found.");

            if (order.CreatedBy != identityId)
                return (null, 403, null);

            var inspection = await dbContext.G25GeneticInspections
                .AsNoTracking()
                .FirstOrDefaultAsync(gi => gi.OrderId == orderId);

            if (inspection is null)
                return (null, 404, "Genetic inspection not found for this order.");

            var distanceResults = await dbContext.G25DistanceResults
                .AsNoTracking()
                .Where(r => r.GeneticInspectionId == inspection.Id)
                .Include(r => r.Era)
                .ToListAsync();

            var admixtureResult = await dbContext.G25AdmixtureResults
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.GeneticInspectionId == inspection.Id);

            var pcaResults = await dbContext.G25PcaResults
                .AsNoTracking()
                .Where(r => r.GeneticInspectionId == inspection.Id)
                .Include(r => r.G25Continent)
                .Include(r => r.PcaFiles)
                    .ThenInclude(f => f.G25PcaFile)
                .ToListAsync();

            var distanceEras = distanceResults
                .OrderBy(r => r.Era.Name)
                .Select(r => new GetOrderG25ResultContract.DistanceEraResult
                {
                    EraId = r.G25EraId,
                    EraName = r.Era.Name,
                    Populations = r.Populations
                        .OrderBy(p => p.Rank)
                        .Select(p => new GetOrderG25ResultContract.DistancePopulationResult
                        {
                            Name = p.Name,
                            Distance = p.Distance,
                            Rank = p.Rank
                        })
                        .ToList()
                })
                .ToList();

            GetOrderG25ResultContract.AdmixtureResult? admixture = null;
            if (admixtureResult is not null)
            {
                admixture = new GetOrderG25ResultContract.AdmixtureResult
                {
                    FitDistance = admixtureResult.FitDistance,
                    Ancestors = admixtureResult.Ancestors
                        .OrderByDescending(a => a.Percentage)
                        .Select(a => new GetOrderG25ResultContract.AdmixtureAncestorResult
                        {
                            Name = a.Name,
                            Percentage = a.Percentage
                        })
                        .ToList()
                };
            }

            var pca = pcaResults
                .OrderBy(r => r.G25Continent.Name)
                .Select(r => new GetOrderG25ResultContract.PcaContinentResult
                {
                    ContinentId = r.G25ContinentId,
                    ContinentName = r.G25Continent.Name,
                    Files = r.PcaFiles
                        .Select(f => new GetOrderG25ResultContract.PcaFileResult
                        {
                            Id = f.G25PcaFileId,
                            FileName = f.G25PcaFile.Title
                        })
                        .ToList()
                })
                .ToList();

            var response = new GetOrderG25ResultContract.Response
            {
                FirstName = inspection.FirstName ?? string.Empty,
                MiddleName = inspection.MiddleName ?? string.Empty,
                LastName = inspection.LastName ?? string.Empty,
                Gender = inspection.Gender?.ToString(),
                HasProfilePicture = inspection.ProfilePicture is { Length: > 0 },
                G25Coordinates = inspection.G25Coordinates,
                DistanceEras = distanceEras,
                Admixture = admixture,
                Pca = pca
            };

            return (response, 200, null);
        }

        public async Task<(byte[]? FileBytes, string? FileName, int StatusCode, string? Error)> DownloadMergedDataForOrderAsync(int orderId, string identityId)
        {
            var order = await dbContext.QpadmOrders
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

        public async Task<(byte[]? FileBytes, string? FileName, int StatusCode, string? Error)> GetProfilePictureAsync(int orderId, string identityId)
        {
            var order = await dbContext.QpadmOrders
                .AsNoTracking()
                .Include(o => o.GeneticInspection)
                .FirstOrDefaultAsync(o => o.Id == orderId);

            if (order is null)
                return (null, null, 404, $"Order with ID {orderId} not found.");

            if (order.CreatedBy != identityId)
                return (null, null, 403, "You do not have permission to access this order's profile picture.");

            if (order.GeneticInspection?.ProfilePicture is not { Length: > 0 } pictureData)
                return (null, null, 404, $"No profile picture found for order with ID {orderId}.");

            return (pictureData, order.GeneticInspection.ProfilePictureFileName ?? "profile-picture", 200, null);
        }

        public async Task<(bool Success, int StatusCode, string? Error)> MarkQpadmResultsAsViewedAsync(int orderId, string identityId)
        {
            var order = await dbContext.QpadmOrders
                .FirstOrDefaultAsync(o => o.Id == orderId);

            if (order is null)
                return (false, 404, $"qpAdm order with ID {orderId} not found.");

            if (order.CreatedBy != identityId)
                return (false, 403, "You do not have permission to modify this order.");

            order.HasViewedResults = true;
            await dbContext.SaveChangesAsync();

            return (true, 200, null);
        }

        public async Task<(bool Success, int StatusCode, string? Error)> MarkG25ResultsAsViewedAsync(int orderId, string identityId)
        {
            var order = await dbContext.G25Orders
                .FirstOrDefaultAsync(o => o.Id == orderId);

            if (order is null)
                return (false, 404, $"G25 order with ID {orderId} not found.");

            if (order.CreatedBy != identityId)
                return (false, 403, "You do not have permission to modify this order.");

            order.HasViewedResults = true;
            await dbContext.SaveChangesAsync();

            return (true, 200, null);
        }

        public async Task<RecomputeG25DistancesContract.Response> RecomputeG25DistanceResultsAsync(string identityId, IReadOnlyList<int>? inspectionIds = null)
        {
            var startedAt = DateTime.UtcNow;
            var stopwatch = Stopwatch.StartNew();
            var version = $"v{startedAt:yyyyMMddHHmmss}";

            var eras = await dbContext.G25Eras
                .AsNoTracking()
                .Where(e => e.DistanceFile != null)
                .Select(e => new { e.Id, e.Name })
                .ToListAsync();

            var response = new RecomputeG25DistancesContract.Response
            {
                Version = version,
                ErasConsidered = eras.Count,
                InspectionsRequested = inspectionIds?.Count ?? 0,
            };

            if (eras.Count == 0)
            {
                logger.LogWarning("G25 distance recompute aborted: no eras with an attached distance file.");
                stopwatch.Stop();
                response.DurationMs = stopwatch.ElapsedMilliseconds;
                return response;
            }

            var inspectionsQuery = dbContext.G25GeneticInspections
                .Where(gi => gi.G25Coordinates != null && gi.G25Coordinates != "");

            if (inspectionIds is { Count: > 0 })
            {
                var idSet = inspectionIds.ToHashSet();
                inspectionsQuery = inspectionsQuery.Where(gi => idSet.Contains(gi.Id));
            }

            var inspections = await inspectionsQuery
                .Select(gi => new { gi.Id, gi.FirstName, gi.LastName, gi.G25Coordinates })
                .ToListAsync();

            if (response.InspectionsRequested == 0)
            {
                response.InspectionsRequested = inspections.Count;
            }

            foreach (var inspection in inspections)
            {
                var existingByEra = await dbContext.G25DistanceResults
                    .Where(r => r.GeneticInspectionId == inspection.Id)
                    .ToDictionaryAsync(r => r.G25EraId);

                var targetName = BuildTargetName(inspection.FirstName, inspection.LastName);
                var normalizedTarget = NormalizeCoordinatesForTarget(inspection.G25Coordinates!, targetName);
                var changedForInspection = 0;

                foreach (var era in eras)
                {
                    var (computeResponse, error, notFound) = await g25CalculationService.ComputeDistancesAsync(
                        new ComputeDistancesContract.Request
                        {
                            TargetCoordinates = normalizedTarget,
                            G25EraId = era.Id,
                            MaxResults = G25DistanceMaxResults
                        });

                    if (computeResponse is null)
                    {
                        logger.LogWarning(
                            "G25 distance recompute failed for inspection {InspectionId}, era {EraId} ({EraName}). NotFound={NotFound}. Error={Error}",
                            inspection.Id, era.Id, era.Name, notFound, error);
                        continue;
                    }

                    if (computeResponse.Results.Count == 0)
                        continue;

                    var populations = computeResponse.Results[0].Rows
                        .Select((row, index) => new G25DistancePopulation
                        {
                            Name = row.Name,
                            Distance = row.Distance,
                            Rank = index + 1
                        })
                        .ToList();

                    if (populations.Count == 0)
                        continue;

                    var now = DateTime.UtcNow;
                    if (existingByEra.TryGetValue(era.Id, out var existing))
                    {
                        existing.Populations = populations;
                        existing.ResultsVersion = version;
                        existing.UpdatedBy = identityId;
                        existing.UpdatedAt = now;
                    }
                    else
                    {
                        dbContext.G25DistanceResults.Add(new G25DistanceResult
                        {
                            GeneticInspectionId = inspection.Id,
                            G25EraId = era.Id,
                            ResultsVersion = version,
                            Populations = populations,
                            CreatedBy = identityId,
                            CreatedAt = now,
                            UpdatedAt = now,
                        });
                    }

                    changedForInspection++;
                }

                if (changedForInspection > 0)
                {
                    await dbContext.SaveChangesAsync();
                    response.InspectionsProcessed++;
                    response.ResultsUpserted += changedForInspection;
                }
                else
                {
                    response.InspectionsSkipped++;
                }
            }

            stopwatch.Stop();
            response.DurationMs = stopwatch.ElapsedMilliseconds;
            logger.LogInformation(
                "G25 distance recompute complete. Version={Version}, Inspections processed={Processed}, skipped={Skipped}, Results upserted={Results}, Duration={DurationMs}ms",
                response.Version, response.InspectionsProcessed, response.InspectionsSkipped, response.ResultsUpserted, response.DurationMs);

            return response;
        }

        public async Task<List<AdminG25InspectionContract.ListItem>> GetAdminG25InspectionsAsync()
        {
            return await dbContext.G25GeneticInspections
                .AsNoTracking()
                .OrderByDescending(gi => gi.Id)
                .Select(gi => new AdminG25InspectionContract.ListItem
                {
                    Id = gi.Id,
                    OrderId = gi.OrderId,
                    FirstName = gi.FirstName,
                    MiddleName = gi.MiddleName,
                    LastName = gi.LastName,
                    UserEmail = gi.User != null ? gi.User.Email : null,
                    HasCoordinates = gi.G25Coordinates != null && gi.G25Coordinates != "",
                    ResultCount = gi.G25DistanceResults.Count,
                    LatestResultsVersion = gi.G25DistanceResults
                        .OrderByDescending(r => r.UpdatedAt)
                        .Select(r => r.ResultsVersion)
                        .FirstOrDefault(),
                    LatestResultsUpdatedAt = gi.G25DistanceResults
                        .OrderByDescending(r => r.UpdatedAt)
                        .Select(r => (DateTime?)r.UpdatedAt)
                        .FirstOrDefault(),
                })
                .ToListAsync();
        }
}
