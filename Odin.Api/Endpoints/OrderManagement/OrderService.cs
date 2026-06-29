using System.Diagnostics;
using System.Text;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Odin.Api.Data;
using Odin.Api.Data.Entities;
using Odin.Api.Data.Enums;
using Odin.Api.Endpoints.Admin.Models;
using Odin.Api.Endpoints.CladeFinderManagement;
using Odin.Api.Endpoints.CladeFinderManagement.Models;
using Odin.Api.Endpoints.G25Calculations;
using Odin.Api.Endpoints.G25Calculations.Models;
using Odin.Api.Endpoints.MergeManagement;
using Odin.Api.Configuration;
using Odin.Api.Endpoints.OrderManagement.Models;
using Odin.Api.Endpoints.Payments;
using Odin.Api.Endpoints.Payments.Models;
using Odin.Api.Extensions;
using Odin.Api.Services;

namespace Odin.Api.Endpoints.OrderManagement;

public interface IOrderService
{
    Task<CreateOrderContract.Response> CreateAsync(CreateOrderContract.Request request, string identityId, string? ipAddress = null);
    /// <summary>
    /// Creates an order gated on a validated Apple StoreKit purchase (iOS in-app purchase). Validates the
    /// signed transaction, then creates the order and records the consumed transaction atomically. Idempotent
    /// on the Apple transaction id: replaying the same transaction returns the order it already created.
    /// </summary>
    Task<CreateOrderContract.Response> CreatePaidAsync(CreateOrderContract.Request request, string identityId, string? ipAddress = null);
    Task<GetOrderContract.Response?> GetByIdAsync(int id, string identityId);
    Task<IEnumerable<GetOrderContract.Response>> GetAllAsync(string identityId);
    Task<IEnumerable<AdminGetOrderContract.Response>> GetAllAdminAsync(int? skip = null, int? take = null);
    Task<(GetOrderContract.Response? Response, int StatusCode)> UpdateAsync(int id, string identityId, UpdateOrderContract.Request request);
    Task<bool> DeleteAsync(int id);
    Task<(GetOrderQpadmResultContract.Response? Result, int StatusCode, string? Error)> GetQpadmResultForOrderAsync(int orderId, string identityId, bool isAdmin = false);
    /// <summary>Validate the StoreKit Y-DNA-unlock purchase, record the per-order entitlement (idempotent on the Apple
    /// transaction id), and return the now-unlocked Y-DNA result. 200 / 400 (bad purchase) / 403 / 404.</summary>
    Task<(GetOrderQpadmResultContract.YDnaResult? YDna, int StatusCode, string? Error)> PurchaseYDnaUnlockAsync(int orderId, string identityId, string transactionJws);
    Task<(GetOrderG25ResultContract.Response? Result, int StatusCode, string? Error)> GetG25ResultForOrderAsync(int orderId, string identityId, bool isAdmin = false);
    Task<(int StatusCode, string? Error, string? MergeId, string? FileName, byte[]? LegacyBytes)> ResolveMergedDataDownloadAsync(int orderId, string identityId, bool isAdmin = false);
    Task<(byte[]? FileBytes, string? FileName, int StatusCode, string? Error)> GetProfilePictureAsync(int orderId, string identityId, ServiceType service, bool isAdmin = false);
    Task<(bool Success, int StatusCode, string? Error)> MarkQpadmResultsAsViewedAsync(int orderId, string identityId, bool isAdmin = false);
    Task<(bool Success, int StatusCode, string? Error)> MarkG25ResultsAsViewedAsync(int orderId, string identityId, bool isAdmin = false);
    Task<RecomputeG25DistancesContract.Response> RecomputeG25DistanceResultsAsync(string identityId, IReadOnlyList<int>? inspectionIds = null);
    Task<List<AdminG25InspectionContract.ListItem>> GetAdminG25InspectionsAsync();
}

/// <summary>
/// Cache keys for the per-order Ancient Origins result payloads. qpAdm and G25 order IDs are
/// independent sequences and can collide, so each service is namespaced. The qpAdm key is also
/// removed by the Y-DNA backfill job when a clade result is (re)computed.
/// </summary>
internal static class OrderResultCacheKeys
{
    internal static string Qpadm(int orderId) => $"order-result:qpadm:{orderId}";
    internal static string G25(int orderId) => $"order-result:g25:{orderId}";
}

public partial class OrderService(
    ApplicationDbContext dbContext,
    IGeoLocationService geoLocationService,
    IG25CalculationService g25CalculationService,
    IBackgroundJobClient backgroundJobClient,
    IOptions<OrderLimitsOptions> orderLimitsOptions,
    IMemoryCache cache,
    Odin.Api.Hubs.IGeneticInspectionRealtimeNotifier liveUpdates,
    Odin.Api.Hubs.IAppStorePurchaseRealtimeNotifier purchaseLiveUpdates,
    IHostEnvironment hostEnvironment,
    IAppStorePurchaseService appStorePurchase,
    IOptions<AppleIapOptions> appleIapOptions,
    ILogger<OrderService> logger) : IOrderService
{
    /// <summary>A server-validated Apple purchase to record against a newly created order.</summary>
    private sealed record PaidPurchase(VerifiedAppStoreTransaction Transaction, decimal Price);

        private const string G25DistanceResultsVersion = "v1";
        private int MaxEthnicities => orderLimitsOptions.Value.MaxEthnicities;
        private int MaxRegionsPerEthnicity => orderLimitsOptions.Value.MaxRegionsPerEthnicity;
        private int G25DistanceMaxResults => orderLimitsOptions.Value.G25DistanceMaxResults;
        private static readonly TimeSpan ResultCacheDuration = TimeSpan.FromDays(5);

        // The qpAdm payload embeds a Y-DNA tab whose status is "Pending" (no clade row yet — a backfill
        // was just enqueued) or "Unavailable" (transient failure — re-enqueued to self-heal) until the
        // background analysis lands. Those states change on a later view, so they must never be frozen in
        // the cache; every other status is terminal. Deny-list form so any future transient status is
        // treated as non-cacheable by default.
        internal static bool IsQpadmResponseCacheable(GetOrderQpadmResultContract.Response response) =>
            response.YDna is { Status: var status }
            && status is not ("Pending" or nameof(CladeAnalysisStatus.Unavailable));

        public Task<CreateOrderContract.Response> CreateAsync(CreateOrderContract.Request request, string identityId, string? ipAddress = null)
            => CreateInternalAsync(request, identityId, ipAddress, paid: null);

        private async Task<CreateOrderContract.Response> CreateInternalAsync(
            CreateOrderContract.Request request, string identityId, string? ipAddress, PaidPurchase? paid)
        {
            var user = await dbContext.Users.RequireByIdentityAsync(identityId);

            if ((user.Country is null || user.CountryCode is null) && ipAddress is not null)
            {
                var geo = await geoLocationService.GetCountryFromIpAsync(ipAddress);
                user.Country = geo?.Country;
                user.CountryCode = geo?.CountryCode;
            }

            if (request.Service == ServiceType.g25)
                return await CreateG25OrderAsync(request, identityId, user, paid);

            await using var transaction = await dbContext.Database.BeginTransactionAsync();

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
                var data = memoryStream.ToArray();

                if (!FileSignatureValidator.LooksLikeGeneticFile(data))
                    throw new InvalidOperationException(
                        "Uploaded file does not look like a valid genetic data file. " +
                        "Expected a vendor raw-data export (23andMe, AncestryDNA, MyHeritage, FTDNA, ...) as text, ZIP, or GZIP.");

                var rawGeneticFile = new RawGeneticFile
                {
                    // Stamp the stored name with the order's subject name + a UTC timestamp so the per-user
                    // (CreatedBy, RawDataFileName WHERE IsDeleted = false) uniqueness index never collides
                    // when the same person re-uploads a same-named file (e.g. "genome.txt") across orders.
                    RawDataFileName = BuildUniqueStoredFileName(request.File!.FileName, request.FirstName, request.LastName),
                    RawData = data,
                    CreatedBy = identityId
                };
                dbContext.RawGeneticFiles.Add(rawGeneticFile);
                await dbContext.SaveChangesAsync();

                rawGeneticFileId = rawGeneticFile.Id;
            }

            var now = DateTime.UtcNow;
            var order = new QpadmOrder
            {
                Price = paid?.Price ?? 0m,
                Status = OrderStatus.Pending,
                CreatedBy = identityId,
                CreatedAt = now,
                UpdatedAt = now,
                UpdatedBy = identityId,
            };
            dbContext.QpadmOrders.Add(order);
            await dbContext.SaveChangesAsync();

            var geneticInspection = new QpadmGeneticInspection
            {
                FirstName = request.FirstName,
                MiddleName = request.MiddleName ?? string.Empty,
                LastName = request.LastName,
                Gender = Enum.TryParse<Data.Enums.Gender>(request.Gender, ignoreCase: true, out var parsedGender)
                    ? parsedGender
                    : throw new InvalidOperationException(
                        $"Invalid gender '{request.Gender}'. Expected 'Male' or 'Female'."),
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

            // FK resolves via the navigation property on save — no intermediate SaveChanges needed.
            var regionAssociations = regions.Select(region => new QpadmGeneticInspectionRegion
            {
                GeneticInspection = geneticInspection,
                Region = region
            }).ToList();

            dbContext.QpadmGeneticInspectionRegions.AddRange(regionAssociations);

            // Record the paid purchase in the SAME transaction as the order, so a successful order always
            // has its consumed AppStoreTransaction row (and vice-versa) — the unique (App, TransactionId)
            // index then makes a replay return this order instead of creating a second one.
            if (paid is not null)
                RecordPurchase(paid, qpadmOrder: order, g25Order: null);

            await dbContext.SaveChangesAsync();
            await transaction.CommitAsync();

            // A new qpAdm order is a new row in the Clients Ancient Origins Results table — push it live.
            await liveUpdates.NotifyChangedAsync("Created", geneticInspection.Id);

            // Compute the Y-DNA clade from the uploaded raw data in the background and cache it, so the
            // "Y-DNA Haplogroup" tab on the result view is ready without recomputing on every view. The
            // clade service is an external call; running it inline would block order creation, so it is
            // enqueued (Hangfire) after the order is safely committed. Enqueue failures must never affect
            // the order — the GET-time backfill recovers any order without a cached clade result.
            EnqueueYDnaCompute(geneticInspection.Id);

            // The order's raw file starts as MergeStatus.NotStarted — i.e. it's now in the logical merge
            // queue. We don't merge it on creation; we just nudge the dispatcher, which admits it only when
            // in-flight merges are below the cap (2). Like the Y-DNA enqueue, a failure here must never break
            // the order — the recurring dispatcher is the backstop.
            EnqueueMergeDispatch();

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
            User user,
            PaidPurchase? paid)
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync();

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
                var data = memoryStream.ToArray();

                if (!FileSignatureValidator.LooksLikeGeneticFile(data))
                    throw new InvalidOperationException(
                        "Uploaded file does not look like a valid genetic data file. " +
                        "Expected a vendor raw-data export (23andMe, AncestryDNA, MyHeritage, FTDNA, ...) as text, ZIP, or GZIP.");

                var rawGeneticFile = new RawGeneticFile
                {
                    // Stamp the stored name with the order's subject name + a UTC timestamp so the per-user
                    // (CreatedBy, RawDataFileName WHERE IsDeleted = false) uniqueness index never collides
                    // when the same person re-uploads a same-named file (e.g. "genome.txt") across orders.
                    RawDataFileName = BuildUniqueStoredFileName(request.File!.FileName, request.FirstName, request.LastName),
                    RawData = data,
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

            var now = DateTime.UtcNow;
            var order = new G25Order
            {
                Price = paid?.Price ?? 0m,
                Status = OrderStatus.Pending,
                CreatedBy = identityId,
                CreatedAt = now,
                UpdatedAt = now,
                UpdatedBy = identityId,
            };
            dbContext.G25Orders.Add(order);
            await dbContext.SaveChangesAsync();

            var geneticInspection = new G25GeneticInspection
            {
                FirstName = request.FirstName,
                MiddleName = request.MiddleName ?? string.Empty,
                LastName = request.LastName,
                Gender = Enum.TryParse<Data.Enums.Gender>(request.Gender, ignoreCase: true, out var parsedGender)
                    ? parsedGender
                    : throw new InvalidOperationException(
                        $"Invalid gender '{request.Gender}'. Expected 'Male' or 'Female'."),
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

            // Record the paid purchase atomically with the order (see the qpAdm path for why).
            if (paid is not null)
                RecordPurchase(paid, qpadmOrder: null, g25Order: order);

            await dbContext.SaveChangesAsync();
            // Commit the order + inspection create before running the distance compute below.
            // The compute is expensive and is a Phase 2 target for background-jobification;
            // failures there leave the order Pending (existing behavior) rather than rolling back.
            await transaction.CommitAsync();

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
                        "G25 order {OrderId} (inspection {InspectionId}) produced no distance results; leaving status Pending. Verify that at least one G25DistanceEra has attached G25DistancePopulationSamples with matching column count.",
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

        public async Task<CreateOrderContract.Response> CreatePaidAsync(
            CreateOrderContract.Request request, string identityId, string? ipAddress = null)
        {
            // 1. Verify the Apple StoreKit transaction (signature + bundle + product↔service). Throws
            //    AppStorePurchaseException (→ 400) on any failure; the iOS app then keeps the StoreKit
            //    transaction unfinished so it can retry rather than losing the purchase.
            var verified = appStorePurchase.ValidateTransaction(
                request.AppStoreTransaction ?? string.Empty, request.Service);

            // 2. Idempotency fast-path: this transaction already created an order — return it unchanged.
            var prior = await FindOrderForTransactionAsync(verified.TransactionId);
            if (prior is not null)
                return prior;

            var price = request.Service == ServiceType.g25
                ? appleIapOptions.Value.G25Price
                : appleIapOptions.Value.QpadmPrice;

            try
            {
                var created = await CreateInternalAsync(request, identityId, ipAddress, new PaidPurchase(verified, price));

                // Live-push the new purchase to the admin "App Store Transactions" page (best-effort; the notifier
                // swallows its own failures so a live-refresh hiccup never fails a paid order).
                await purchaseLiveUpdates.NotifyPurchaseRecordedAsync(
                    kind: "Order",
                    productLabel: request.Service == ServiceType.g25 ? "G25 Analysis" : "qpAdm Analysis",
                    amount: price,
                    currency: appleIapOptions.Value.Currency,
                    createdBySub: identityId);

                return created;
            }
            catch (DbUpdateException)
            {
                // Lost a race to insert the same transaction (unique (App, TransactionId)). The winner's
                // order is committed — return it so the purchase still resolves to exactly one order.
                var raced = await FindOrderForTransactionAsync(verified.TransactionId);
                if (raced is not null)
                    return raced;
                throw;
            }
        }

        /// <summary>Returns the order a consumed Apple transaction already created, or null if none.</summary>
        private async Task<CreateOrderContract.Response?> FindOrderForTransactionAsync(string transactionId)
        {
            var txn = await dbContext.AppStoreTransactions
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.TransactionId == transactionId);
            if (txn is null)
                return null;

            if (txn.Service == ServiceType.g25 && txn.G25OrderId is { } g25OrderId)
            {
                var order = await dbContext.G25Orders
                    .AsNoTracking()
                    .Include(o => o.GeneticInspection)
                    .FirstOrDefaultAsync(o => o.Id == g25OrderId);
                return order is null ? null : new CreateOrderContract.Response
                {
                    Id = order.Id,
                    Price = order.Price,
                    Service = ServiceType.g25.ToString(),
                    Status = order.Status.ToString(),
                    GeneticInspectionId = order.GeneticInspection?.Id ?? 0,
                };
            }

            if (txn.Service == ServiceType.qpAdm && txn.QpadmOrderId is { } qpadmOrderId)
            {
                var order = await dbContext.QpadmOrders
                    .AsNoTracking()
                    .Include(o => o.GeneticInspection)
                    .FirstOrDefaultAsync(o => o.Id == qpadmOrderId);
                return order is null ? null : new CreateOrderContract.Response
                {
                    Id = order.Id,
                    Price = order.Price,
                    Service = ServiceType.qpAdm.ToString(),
                    Status = order.Status.ToString(),
                    GeneticInspectionId = order.GeneticInspection?.Id ?? 0,
                };
            }

            return null;
        }

        /// <summary>
        /// Adds the consumed <see cref="AppStoreTransaction"/> row for a paid order. Exactly one of
        /// <paramref name="qpadmOrder"/> / <paramref name="g25Order"/> is set; the order must already have a
        /// database id (it is saved before this is called).
        /// </summary>
        private void RecordPurchase(PaidPurchase paid, QpadmOrder? qpadmOrder, G25Order? g25Order)
        {
            dbContext.AppStoreTransactions.Add(new AppStoreTransaction
            {
                TransactionId = paid.Transaction.TransactionId,
                OriginalTransactionId = paid.Transaction.OriginalTransactionId,
                ProductId = paid.Transaction.ProductId,
                Service = paid.Transaction.Service,
                Status = AppStoreTransactionStatus.Consumed,
                QpadmOrderId = qpadmOrder?.Id,
                G25OrderId = g25Order?.Id,
                PurchaseDate = paid.Transaction.PurchaseDate,
                Environment = paid.Transaction.Environment,
                RawJws = paid.Transaction.RawJws,
                CreatedBy = qpadmOrder?.CreatedBy ?? g25Order?.CreatedBy ?? string.Empty,
            });
        }

        private async Task<int> ComputeAndPersistG25DistancesAsync(
            int geneticInspectionId,
            string coordinates,
            string firstName,
            string lastName,
            string identityId)
        {
            var eras = await dbContext.G25DistanceEras
                .AsNoTracking()
                .Where(e => e.G25DistancePopulationSamples.Any())
                .Select(e => new { e.Id, e.Name })
                .ToListAsync();

            if (eras.Count == 0)
            {
                logger.LogWarning(
                    "G25 distance compute skipped for inspection {InspectionId}: no G25DistanceEras have attached G25DistancePopulationSamples.",
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
                        G25DistanceEraId = era.Id,
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
                    G25DistanceEraId = era.Id,
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

        private static string NextG25DistanceVersion(string? current)
        {
            var n = ParseG25DistanceVersionNumber(current);
            return n > 0 ? $"v{n + 1}" : "v1";
        }

        private static int ParseG25DistanceVersionNumber(string? version)
        {
            if (string.IsNullOrWhiteSpace(version) ||
                !version.StartsWith("v", StringComparison.OrdinalIgnoreCase))
                return 0;
            return int.TryParse(version.AsSpan(1), out var n) && n > 0 ? n : 0;
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

        public async Task<IEnumerable<AdminGetOrderContract.Response>> GetAllAdminAsync(int? skip = null, int? take = null)
        {
            // When paging, only the newest (skip+take) rows from EACH table can contribute to the page:
            // the globally newest N orders are necessarily within each table's own newest N. So bound
            // the per-table DB read instead of materializing both full tables (the ORDER BY + LIMIT run
            // in Postgres, served by the (CreatedAt) index). When take is null, behaviour is unchanged.
            int? perTableLimit = take is null
                ? null
                : Math.Max(0, skip ?? 0) + Math.Clamp(take.Value, 1, 500);

            // Project owner info from application_users via a left join on CreatedBy → IdentityId so
            // orders whose creator was never provisioned still appear (with null OwnerId/Email).
            IQueryable<AdminGetOrderContract.Response> qpadmQuery =
                from order in dbContext.QpadmOrders.AsNoTracking()
                join u in dbContext.Users.AsNoTracking()
                    on order.CreatedBy equals u.IdentityId into userJoin
                from owner in userJoin.DefaultIfEmpty()
                select new AdminGetOrderContract.Response
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
                    HasProfilePicture = order.GeneticInspection != null
                        && order.GeneticInspection.ProfilePicture != null
                        && order.GeneticInspection.ProfilePicture.Length > 0,
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
                    UpdatedBy = order.UpdatedBy,
                    OwnerId = owner != null ? owner.Id : (int?)null,
                    OwnerEmail = owner != null ? owner.Email : null,
                    OwnerFirstName = owner != null ? owner.FirstName : string.Empty,
                    OwnerLastName = owner != null ? owner.LastName : string.Empty,
                };
            if (perTableLimit is { } qpadmLimit)
                qpadmQuery = qpadmQuery.OrderByDescending(o => o.CreatedAt).Take(qpadmLimit);
            var qpadmOrders = await qpadmQuery.ToListAsync();

            IQueryable<AdminGetOrderContract.Response> g25Query =
                from order in dbContext.G25Orders.AsNoTracking()
                join u in dbContext.Users.AsNoTracking()
                    on order.CreatedBy equals u.IdentityId into userJoin
                from owner in userJoin.DefaultIfEmpty()
                select new AdminGetOrderContract.Response
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
                    HasProfilePicture = order.GeneticInspection != null
                        && order.GeneticInspection.ProfilePicture != null
                        && order.GeneticInspection.ProfilePicture.Length > 0,
                    HasViewedResults = order.HasViewedResults,
                    RegionIds = new List<int>(),
                    EthnicityIds = new List<int>(),
                    CreatedAt = order.CreatedAt,
                    CreatedBy = order.CreatedBy,
                    UpdatedAt = order.UpdatedAt,
                    UpdatedBy = order.UpdatedBy,
                    OwnerId = owner != null ? owner.Id : (int?)null,
                    OwnerEmail = owner != null ? owner.Email : null,
                    OwnerFirstName = owner != null ? owner.FirstName : string.Empty,
                    OwnerLastName = owner != null ? owner.LastName : string.Empty,
                };
            if (perTableLimit is { } g25Limit)
                g25Query = g25Query.OrderByDescending(o => o.CreatedAt).Take(g25Limit);
            var g25Orders = await g25Query.ToListAsync();

            var combined = qpadmOrders.Concat(g25Orders).OrderByDescending(o => o.CreatedAt);
            // Optional, additive paging: when `take` is omitted the full list is returned (unchanged
            // behaviour, so existing callers aren't affected). When provided, each table was already
            // bounded to the newest (skip+take) rows at the DB above, so this final merge/slice runs
            // over at most 2*(skip+take) rows in memory rather than the whole tables.
            if (take is null)
                return combined.ToList();
            return combined.Skip(Math.Max(0, skip ?? 0)).Take(Math.Clamp(take.Value, 1, 500)).ToList();
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
            await liveUpdates.NotifyChangedAsync("Updated", order.GeneticInspection.Id);

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
            cache.Remove(OrderResultCacheKeys.Qpadm(id));
            // The row (and its inspection) is gone — refresh the table. Only the order id is in scope here,
            // which is fine: the FE refetches the whole list rather than a single row.
            await liveUpdates.NotifyChangedAsync("Deleted");
            return true;
        }

        public async Task<(GetOrderQpadmResultContract.Response? Result, int StatusCode, string? Error)> GetQpadmResultForOrderAsync(int orderId, string identityId, bool isAdmin = false)
        {
            var order = await dbContext.QpadmOrders
                .AsNoTracking()
                .Include(o => o.GeneticInspection)
                    .ThenInclude(gi => gi!.RawGeneticFile)
                .FirstOrDefaultAsync(o => o.Id == orderId);

            if (order is null)
                return (null, 404, $"Order with ID {orderId} not found.");

            if (!isAdmin && order.CreatedBy != identityId)
                return (null, 403, "You do not have permission to view this order's results.");

            if (order.Status != OrderStatus.Completed)
                return (null, 400, "Results are only available for completed orders.");

            if (order.GeneticInspection is null)
                return (null, 404, "No genetic inspection associated with this order.");

            if (!hostEnvironment.IsEnvironment("Testing") &&
                cache.TryGetValue(OrderResultCacheKeys.Qpadm(orderId), out GetOrderQpadmResultContract.Response? cachedResponse))
                return (await ApplyYDnaLockAsync(cachedResponse!, orderId, isAdmin), 200, null);

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

            var yDna = await BuildYDnaResultAsync(
                order.GeneticInspection.Id, order.GeneticInspection.Gender?.ToString());

            var response = new GetOrderQpadmResultContract.Response
            {
                FirstName = order.GeneticInspection.FirstName,
                MiddleName = order.GeneticInspection.MiddleName,
                LastName = order.GeneticInspection.LastName,
                HasMergedRawData =
                    order.GeneticInspection.RawGeneticFile is { MergeStatus: MergeStatus.Ready, MergeId.Length: > 0 }
                    || order.GeneticInspection.RawGeneticFile?.MergedRawData is { Length: > 0 },
                HasProfilePicture = order.GeneticInspection.ProfilePicture is { Length: > 0 },
                Gender = order.GeneticInspection.Gender?.ToString(),
                IntroTrackId = introTrack?.Id,
                HasIntroAudioFile = introTrack?.HasFile ?? false,
                ResultsVersion = qpadmResult.ResultsVersion,
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
                            HasVideoAvatar = qrp.Population.VideoAvatarVersion != null,
                            VideoVersion = qrp.Population.VideoAvatarVersion != null
                                ? qrp.Population.VideoAvatarVersion.Value.ToString()
                                : null,
                            Percentage = qrp.Percentage,
                            StandardError = qrp.StandardError,
                            ZScore = qrp.ZScore,
                        }).ToList()
                }).ToList(),
                YDna = yDna
            };

            if (!hostEnvironment.IsEnvironment("Testing") && IsQpadmResponseCacheable(response))
            {
                cache.Set(OrderResultCacheKeys.Qpadm(orderId), response, new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = ResultCacheDuration
                });
            }

            // The CACHE holds the full result (with the clade); the paid Y-DNA lock is applied per-request from the live
            // entitlement, so a purchase takes effect immediately (no cache invalidation) and an admin's unlocked view
            // can't poison the owner's cached (locked) view.
            return (await ApplyYDnaLockAsync(response, orderId, isAdmin), 200, null);
        }

        /// <summary>True once the order's Y-DNA unlock ($9.99) has been purchased.</summary>
        private Task<bool> IsYDnaUnlockedAsync(int orderId) =>
            dbContext.QpadmYDnaUnlocks.AsNoTracking().AnyAsync(u => u.OrderId == orderId);

        /// <summary>
        /// Withholds the Y-DNA clade behind the paid unlock: when there's a real Completed clade but no purchased unlock
        /// (and the caller isn't an admin), returns a COPY of the response whose <c>YDna</c> has the clade stripped and
        /// <c>Locked = true</c>. Never mutates the (shared, cached) input. Info states (NotApplicable/NoYData/Pending/…)
        /// are free — only a sellable Completed clade is locked.
        /// </summary>
        private async Task<GetOrderQpadmResultContract.Response> ApplyYDnaLockAsync(
            GetOrderQpadmResultContract.Response response, int orderId, bool isAdmin)
        {
            var yDna = response.YDna;
            // Only paywall a real, determinable haplogroup. Admins see all; info states and a Completed-but-
            // "couldn't determine" clade (error / empty clade id) are free — there's nothing of value to sell.
            if (isAdmin || yDna is null
                || yDna.Status != nameof(CladeAnalysisStatus.Completed)
                || yDna.Clade is null
                || string.IsNullOrWhiteSpace(yDna.Clade.Clade))
                return response;

            if (await IsYDnaUnlockedAsync(orderId))
                return response;

            return new GetOrderQpadmResultContract.Response
            {
                FirstName = response.FirstName,
                MiddleName = response.MiddleName,
                LastName = response.LastName,
                HasMergedRawData = response.HasMergedRawData,
                HasProfilePicture = response.HasProfilePicture,
                Gender = response.Gender,
                IntroTrackId = response.IntroTrackId,
                HasIntroAudioFile = response.HasIntroAudioFile,
                ResultsVersion = response.ResultsVersion,
                EraGroups = response.EraGroups,   // shared by reference — read-only output, never mutated
                YDna = new GetOrderQpadmResultContract.YDnaResult
                {
                    Status = yDna.Status,
                    Message = yDna.Message,
                    Clade = null,      // withheld until purchased
                    Locked = true,
                },
            };
        }

        public async Task<(GetOrderQpadmResultContract.YDnaResult? YDna, int StatusCode, string? Error)> PurchaseYDnaUnlockAsync(
            int orderId, string identityId, string transactionJws)
        {
            var order = await dbContext.QpadmOrders.AsNoTracking()
                .Include(o => o.GeneticInspection)
                .FirstOrDefaultAsync(o => o.Id == orderId);
            if (order is null)
                return (null, 404, $"Order with ID {orderId} not found.");
            if (order.CreatedBy != identityId)
                return (null, 403, "You do not have permission to unlock this order.");
            if (order.GeneticInspection is null)
                return (null, 404, "No genetic inspection associated with this order.");

            // Validate the StoreKit add-on purchase (throws AppStorePurchaseException → 400 at the endpoint).
            var verified = appStorePurchase.ValidateAddOnTransaction(transactionJws, appleIapOptions.Value.YDnaProductId);

            // A transaction unlocks EXACTLY ONE order — the one it was first redeemed against. The Apple
            // transaction carries no order binding, so we bind it here. **Reject a replay against a DIFFERENT order**
            // (otherwise one $9.99 purchase could reveal every order the buyer owns by re-POSTing the same receipt to
            // each order's endpoint — and the response would return that order's clade for free). A replay against
            // the SAME order is idempotent (retry / app-killed mid-flow).
            var existing = await dbContext.QpadmYDnaUnlocks.AsNoTracking()
                .FirstOrDefaultAsync(u => u.TransactionId == verified.TransactionId);
            if (existing is not null && existing.OrderId != orderId)
                return (null, 400, "This purchase has already been used to unlock a different order.");

            if (existing is null)
            {
                var user = await dbContext.Users.RequireByIdentityAsync(identityId);
                var now = DateTime.UtcNow;
                dbContext.QpadmYDnaUnlocks.Add(new QpadmYDnaUnlock
                {
                    OrderId = orderId,
                    UserId = user.Id,
                    TransactionId = verified.TransactionId,
                    CreatedBy = identityId,
                    CreatedAt = now,
                    UpdatedAt = now,
                });
                try
                {
                    await dbContext.SaveChangesAsync();

                    // Live-push the new add-on purchase to the admin "App Store Transactions" page (best-effort).
                    await purchaseLiveUpdates.NotifyPurchaseRecordedAsync(
                        kind: "YDnaUnlock",
                        productLabel: "Y-DNA Unlock",
                        amount: appleIapOptions.Value.YDnaPrice,
                        currency: appleIapOptions.Value.Currency,
                        createdBySub: identityId);
                }
                catch (DbUpdateException)
                {
                    // A concurrent request replaying the SAME transaction won the unique-TransactionId race. Re-read it:
                    // if it bound to a different order, reject; if to this order, it's an idempotent same-order replay.
                    dbContext.ChangeTracker.Clear();
                    var winner = await dbContext.QpadmYDnaUnlocks.AsNoTracking()
                        .FirstOrDefaultAsync(u => u.TransactionId == verified.TransactionId);
                    if (winner is not null && winner.OrderId != orderId)
                        return (null, 400, "This purchase has already been used to unlock a different order.");
                }
            }

            // The transaction is now bound to THIS order ⇒ return the unlocked Y-DNA result so the client reveals it
            // immediately. (The clade is only returned because the order is genuinely entitled — never on a cross-order
            // replay, which returned 400 above.)
            var yDna = await BuildYDnaResultAsync(
                order.GeneticInspection.Id, order.GeneticInspection.Gender?.ToString());
            return (yDna, 200, null);
        }

        /// <summary>
        /// Enqueues the background Y-DNA clade computation for an inspection. Enqueue failures (e.g. a
        /// Hangfire storage hiccup) are swallowed: they must never break order creation or a result view,
        /// and the GET-time backfill re-attempts any inspection that lacks a cached clade result.
        /// </summary>
        /// <summary>
        /// Builds the stored file name for an uploaded genetic file, appending the order subject's first +
        /// last name and a UTC timestamp so the per-user (CreatedBy, RawDataFileName) uniqueness index can
        /// never collide when the same person uploads a same-named file across multiple orders. The original
        /// extension is preserved (downstream merge/convert keys off it) and the result is capped at the
        /// column's 200-char limit by trimming the original stem, never the uniqueness suffix.
        /// </summary>
        private static string BuildUniqueStoredFileName(string originalFileName, string firstName, string lastName)
        {
            const int maxLength = 200; // matches RawGeneticFileConfiguration.RawDataFileName max length

            var extension = Path.GetExtension(originalFileName);
            var stem = Path.GetFileNameWithoutExtension(originalFileName);

            var who = string.Concat($"{firstName}{lastName}".Where(char.IsLetterOrDigit));
            if (string.IsNullOrEmpty(who)) who = "user";

            var suffix = $"-{who}-{DateTime.UtcNow:yyyyMMddHHmmssfff}{extension}";

            var stemBudget = Math.Max(0, maxLength - suffix.Length);
            if (stem.Length > stemBudget) stem = stem[..stemBudget];

            return $"{stem}{suffix}";
        }

        private void EnqueueYDnaCompute(int geneticInspectionId)
        {
            try
            {
                backgroundJobClient.Enqueue<IYHaplogroupComputeService>(
                    svc => svc.ComputeAndPersistAsync(geneticInspectionId, CancellationToken.None));
            }
            catch (Exception ex)
            {
                logger.LogError(ex,
                    "Failed to enqueue Y-DNA compute for inspection {InspectionId}; it will be backfilled on the next result view.",
                    geneticInspectionId);
            }
        }

        /// <summary>
        /// Nudges the merge dispatcher to admit waiting orders (up to the in-flight cap). The newly created
        /// order is already in the queue via its raw file's <c>NotStarted</c> status, so this only triggers a
        /// dispatch pass. Failures are swallowed so they never break order creation; the recurring dispatcher
        /// is the backstop.
        /// </summary>
        private void EnqueueMergeDispatch()
        {
            try
            {
                backgroundJobClient.Enqueue<IMergeJob>(svc => svc.DispatchPendingMergesAsync(CancellationToken.None));
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to enqueue merge dispatch after order creation.");
            }
        }

        /// <summary>
        /// Builds the Y-DNA tab payload from the cached <see cref="QpadmCladeResult"/>. When no cache exists
        /// (legacy / not-yet-computed orders) or a prior attempt was transiently <c>Unavailable</c>, it
        /// enqueues a background backfill so the next view is served from cache. Female kits resolve to
        /// <c>NotApplicable</c> without any service call.
        /// </summary>
        private async Task<GetOrderQpadmResultContract.YDnaResult> BuildYDnaResultAsync(
            int geneticInspectionId, string? gender)
        {
            var record = await dbContext.QpadmCladeResults
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.GeneticInspectionId == geneticInspectionId);

            var isFemale = string.Equals(gender, nameof(Gender.Female), StringComparison.OrdinalIgnoreCase);

            if (record is null)
            {
                if (isFemale)
                    return new GetOrderQpadmResultContract.YDnaResult
                    {
                        Status = nameof(CladeAnalysisStatus.NotApplicable),
                        Message = "Y-DNA follows the direct paternal line and is only present in male kits, " +
                                  "so a Y-DNA haplogroup can't be determined for this sample.",
                    };

                EnqueueYDnaCompute(geneticInspectionId);
                return new GetOrderQpadmResultContract.YDnaResult
                {
                    Status = "Pending",
                    Message = "Your Y-DNA analysis is being prepared — check back shortly.",
                };
            }

            // A transient failure self-heals on view: re-enqueue while still showing the cached message.
            if (record.Status == CladeAnalysisStatus.Unavailable && !isFemale)
                EnqueueYDnaCompute(geneticInspectionId);

            return new GetOrderQpadmResultContract.YDnaResult
            {
                Status = record.Status.ToString(),
                Message = record.Message,
                Clade = record.Status == CladeAnalysisStatus.Completed
                    ? new AnalyzeCladeContract.Response
                    {
                        Clade = record.Clade,
                        Score = record.Score,
                        NextPrediction = record.NextPredictionClade is null
                            ? null
                            : new AnalyzeCladeContract.NextPrediction
                            {
                                Clade = record.NextPredictionClade,
                                Score = record.NextPredictionScore ?? 0,
                            },
                        Downstream = record.Downstream
                            .Select(d => new AnalyzeCladeContract.DownstreamClade { Clade = d.Clade, Children = d.Children })
                            .ToList(),
                        Lineage = record.Lineage.ToList(),
                        Warning = record.Warning,
                        Error = record.Error,
                        PositivesUsed = record.PositivesUsed,
                        NegativesUsed = record.NegativesUsed,
                        YReads = record.YReads,
                        SourceFormat = record.SourceFormat,
                        EffectiveBuild = record.EffectiveBuild,
                    }
                    : null,
            };
        }

        public async Task<(GetOrderG25ResultContract.Response? Result, int StatusCode, string? Error)> GetG25ResultForOrderAsync(int orderId, string identityId, bool isAdmin = false)
        {
            var order = await dbContext.G25Orders
                .AsNoTracking()
                .FirstOrDefaultAsync(o => o.Id == orderId);

            if (order is null)
                return (null, 404, $"Order with ID {orderId} not found.");

            if (!isAdmin && order.CreatedBy != identityId)
                return (null, 403, null);

            if (!hostEnvironment.IsEnvironment("Testing") &&
                cache.TryGetValue(OrderResultCacheKeys.G25(orderId), out GetOrderG25ResultContract.Response? cachedResponse))
                return (cachedResponse!, 200, null);

            var inspection = await dbContext.G25GeneticInspections
                .AsNoTracking()
                .FirstOrDefaultAsync(gi => gi.OrderId == orderId);

            if (inspection is null)
                return (null, 404, "Genetic inspection not found for this order.");

            var distanceResults = await dbContext.G25DistanceResults
                .AsNoTracking()
                .Where(r => r.GeneticInspectionId == inspection.Id)
                .Include(r => r.DistanceEra)
                .ToListAsync();

            var admixtureResult = await dbContext.G25AdmixtureResults
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.GeneticInspectionId == inspection.Id);

            var pcaResults = await dbContext.G25PcaResults
                .AsNoTracking()
                .Where(r => r.GeneticInspectionId == inspection.Id)
                .Include(r => r.G25Continent)
                .ToListAsync();

            var distanceEras = distanceResults
                .OrderBy(r => r.DistanceEra.Name)
                .Select(r => new GetOrderG25ResultContract.DistanceEraResult
                {
                    EraId = r.G25DistanceEraId,
                    EraName = r.DistanceEra.Name,
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

            if (!hostEnvironment.IsEnvironment("Testing")
                && order.Status == OrderStatus.Completed
                && distanceResults.Count > 0)
            {
                cache.Set(OrderResultCacheKeys.G25(orderId), response, new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = ResultCacheDuration
                });
            }

            return (response, 200, null);
        }

        /// <summary>
        /// Authorizes the caller and resolves where this order's merged dataset lives. The automated merge
        /// stores the bundle on the tools-api volume (returns <c>MergeId</c> for the endpoint to stream);
        /// a legacy hand-uploaded blob is returned inline (<c>LegacyBytes</c>). The endpoint turns whichever
        /// is set into the response, so the multi-GB stream never buffers through this service.
        /// </summary>
        public async Task<(int StatusCode, string? Error, string? MergeId, string? FileName, byte[]? LegacyBytes)>
            ResolveMergedDataDownloadAsync(int orderId, string identityId, bool isAdmin = false)
        {
            var order = await dbContext.QpadmOrders
                .AsNoTracking()
                .Include(o => o.GeneticInspection)
                    .ThenInclude(gi => gi!.RawGeneticFile)
                .FirstOrDefaultAsync(o => o.Id == orderId);

            if (order is null)
                return (404, $"Order with ID {orderId} not found.", null, null, null);

            if (!isAdmin && order.CreatedBy != identityId)
                return (403, "You do not have permission to access this order's data.", null, null, null);

            var file = order.GeneticInspection?.RawGeneticFile;

            // Preferred: the automated merge bundle on the tools-api volume.
            if (file is { MergeStatus: MergeStatus.Ready, MergeId: { Length: > 0 } mergeId })
            {
                var name = string.IsNullOrWhiteSpace(file.MergeFileName) ? $"{mergeId}.tar.gz" : file.MergeFileName;
                return (200, null, mergeId, name, null);
            }

            // Legacy: a merged dataset that was uploaded by hand into the DB blob.
            if (file?.MergedRawData is { Length: > 0 } legacy)
            {
                var name = file.MergedRawDataFileName ?? "merged-data";
                return (200, null, null, name, legacy);
            }

            return (404, "No merged data available for this order.", null, null, null);
        }

        public async Task<(byte[]? FileBytes, string? FileName, int StatusCode, string? Error)> GetProfilePictureAsync(int orderId, string identityId, ServiceType service, bool isAdmin = false)
        {
            // qpAdm and G25 orders live in separate tables with independent identity sequences, so the same
            // numeric ID can exist in both. The caller's service type disambiguates which table to read from
            // — querying the wrong one would 404 (G25 orders were previously invisible here) or, on an ID
            // collision, return the other order's picture.
            string? createdBy;
            byte[]? pictureData;
            string? pictureFileName;

            if (service == ServiceType.g25)
            {
                var order = await dbContext.G25Orders
                    .AsNoTracking()
                    .Include(o => o.GeneticInspection)
                    .FirstOrDefaultAsync(o => o.Id == orderId);

                if (order is null)
                    return (null, null, 404, $"Order with ID {orderId} not found.");

                createdBy = order.CreatedBy;
                pictureData = order.GeneticInspection?.ProfilePicture;
                pictureFileName = order.GeneticInspection?.ProfilePictureFileName;
            }
            else
            {
                var order = await dbContext.QpadmOrders
                    .AsNoTracking()
                    .Include(o => o.GeneticInspection)
                    .FirstOrDefaultAsync(o => o.Id == orderId);

                if (order is null)
                    return (null, null, 404, $"Order with ID {orderId} not found.");

                createdBy = order.CreatedBy;
                pictureData = order.GeneticInspection?.ProfilePicture;
                pictureFileName = order.GeneticInspection?.ProfilePictureFileName;
            }

            if (!isAdmin && createdBy != identityId)
                return (null, null, 403, "You do not have permission to access this order's profile picture.");

            if (pictureData is not { Length: > 0 })
                return (null, null, 404, $"No profile picture found for order with ID {orderId}.");

            return (pictureData, pictureFileName ?? "profile-picture", 200, null);
        }

        public async Task<(bool Success, int StatusCode, string? Error)> MarkQpadmResultsAsViewedAsync(int orderId, string identityId, bool isAdmin = false)
        {
            var order = await dbContext.QpadmOrders
                .FirstOrDefaultAsync(o => o.Id == orderId);

            if (order is null)
                return (false, 404, $"qpAdm order with ID {orderId} not found.");

            if (order.CreatedBy != identityId)
            {
                // Admin previewing another user's results must not consume their "New Results" indicator.
                if (isAdmin)
                    return (true, 200, null);
                return (false, 403, "You do not have permission to modify this order.");
            }

            order.HasViewedResults = true;
            await dbContext.SaveChangesAsync();

            return (true, 200, null);
        }

        public async Task<(bool Success, int StatusCode, string? Error)> MarkG25ResultsAsViewedAsync(int orderId, string identityId, bool isAdmin = false)
        {
            var order = await dbContext.G25Orders
                .FirstOrDefaultAsync(o => o.Id == orderId);

            if (order is null)
                return (false, 404, $"G25 order with ID {orderId} not found.");

            if (order.CreatedBy != identityId)
            {
                if (isAdmin)
                    return (true, 200, null);
                return (false, 403, "You do not have permission to modify this order.");
            }

            order.HasViewedResults = true;
            await dbContext.SaveChangesAsync();

            return (true, 200, null);
        }

}
