using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Odin.Api.Data;
using Odin.Api.Data.Entities;
using Odin.Api.Data.Enums;
using Odin.Api.Services.Paddle.Models.Prices;
using Odin.Api.Services.Paddle.Models.Products;
using Odin.Api.Services.Paddle.Resources;

namespace Odin.Api.Services.Paddle.Sync;

public interface IPaddleProductSyncService
{
    /// <summary>
    /// Pulls every product (and its prices via <c>?include=prices</c>) from Paddle and upserts
    /// them into <see cref="ApplicationDbContext.PaddleProducts"/> and
    /// <see cref="ApplicationDbContext.PaddlePrices"/>. Discriminator columns
    /// (<c>kind</c>, <c>service_type</c>, <c>parent_service_type</c>, <c>addon_code</c>) are
    /// projected from each product's <c>custom_data</c>.
    /// </summary>
    Task<PaddleSyncResult> SyncAllAsync(CancellationToken cancellationToken = default);

    /// <summary>Pulls and upserts a single product (with prices) by Paddle id.</summary>
    Task<PaddleSyncResult> SyncOneAsync(string paddleProductId, CancellationToken cancellationToken = default);
}

public sealed class PaddleProductSyncService(
    IPaddleProductsResource productsResource,
    ApplicationDbContext dbContext,
    ILogger<PaddleProductSyncService> logger) : IPaddleProductSyncService
{
    public async Task<PaddleSyncResult> SyncAllAsync(CancellationToken cancellationToken = default)
    {
        var result = new PaddleSyncResult { Resource = "products" };

        var existingProducts = await dbContext.PaddleProducts
            .Include(p => p.Prices)
            .ToDictionaryAsync(p => p.PaddleProductId, cancellationToken);

        await foreach (var dto in productsResource.ListAllAsync(
            new PaddleProductListQuery { IncludePrices = true, PerPage = 200 }, cancellationToken))
        {
            try
            {
                UpsertProduct(dto, existingProducts, result);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Paddle product sync: failed to project product {ProductId}.", dto.Id);
                result.Failed++;
                result.Errors.Add($"{dto.Id}: {ex.Message}");
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Paddle product sync complete: inserted={Inserted}, updated={Updated}, failed={Failed}.",
            result.Inserted, result.Updated, result.Failed);

        return result;
    }

    public async Task<PaddleSyncResult> SyncOneAsync(string paddleProductId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(paddleProductId);

        var result = new PaddleSyncResult { Resource = "products" };
        var dto = await productsResource.GetAsync(paddleProductId, includePrices: true, cancellationToken);

        var existing = await dbContext.PaddleProducts
            .Include(p => p.Prices)
            .Where(p => p.PaddleProductId == paddleProductId)
            .ToDictionaryAsync(p => p.PaddleProductId, cancellationToken);

        UpsertProduct(dto, existing, result);
        await dbContext.SaveChangesAsync(cancellationToken);
        return result;
    }

    private void UpsertProduct(
        PaddleProductDto dto,
        Dictionary<string, PaddleProduct> existing,
        PaddleSyncResult result)
    {
        var now = DateTime.UtcNow;
        var customData = dto.CustomData?.GetRawText();
        var (kind, serviceType, parentServiceType, addonCode) = ParseDiscriminators(dto);

        if (existing.TryGetValue(dto.Id, out var entity))
        {
            entity.Name = dto.Name;
            entity.Description = dto.Description;
            entity.Type = dto.Type;
            entity.TaxCategory = dto.TaxCategory;
            entity.ImageUrl = dto.ImageUrl;
            entity.Status = dto.Status ?? "active";
            entity.Kind = kind;
            entity.ServiceType = serviceType;
            entity.ParentServiceType = parentServiceType;
            entity.AddonCode = addonCode;
            entity.CustomData = customData;
            entity.PaddleCreatedAt = dto.CreatedAt;
            entity.PaddleUpdatedAt = dto.UpdatedAt;
            entity.LastSyncedAt = now;
            result.Updated++;
        }
        else
        {
            entity = new PaddleProduct
            {
                PaddleProductId = dto.Id,
                Name = dto.Name,
                Description = dto.Description,
                Type = dto.Type,
                TaxCategory = dto.TaxCategory,
                ImageUrl = dto.ImageUrl,
                Status = dto.Status ?? "active",
                Kind = kind,
                ServiceType = serviceType,
                ParentServiceType = parentServiceType,
                AddonCode = addonCode,
                CustomData = customData,
                PaddleCreatedAt = dto.CreatedAt,
                PaddleUpdatedAt = dto.UpdatedAt,
                LastSyncedAt = now,
            };
            dbContext.PaddleProducts.Add(entity);
            existing[dto.Id] = entity;
            result.Inserted++;
        }

        if (dto.Prices is { Count: > 0 })
            UpsertPrices(entity, dto.Prices, now);
    }

    private static (string? kind, ServiceType? serviceType, ServiceType? parentServiceType, string? addonCode) ParseDiscriminators(PaddleProductDto dto)
    {
        if (dto.CustomData is not { ValueKind: JsonValueKind.Object } cd)
            return (null, null, null, null);

        var kind = TryGetString(cd, "kind");
        var serviceType = ParseServiceType(TryGetString(cd, "service_type"));
        var parentServiceType = ParseServiceType(TryGetString(cd, "parent_service_type"));
        var addonCode = TryGetString(cd, "addon_code");
        return (kind, serviceType, parentServiceType, addonCode);
    }

    private static ServiceType? ParseServiceType(string? raw) =>
        Enum.TryParse<ServiceType>(raw, ignoreCase: true, out var v) ? v : null;

    private static string? TryGetString(JsonElement obj, string name) =>
        obj.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String
            ? v.GetString()
            : null;

    private void UpsertPrices(PaddleProduct product, List<PaddlePriceDto> prices, DateTime now)
    {
        var byId = product.Prices.ToDictionary(p => p.PaddlePriceId, StringComparer.Ordinal);

        foreach (var dto in prices)
        {
            var customData = dto.CustomData?.GetRawText();

            if (byId.TryGetValue(dto.Id, out var existing))
            {
                existing.Description = dto.Description;
                existing.Name = dto.Name;
                existing.Type = dto.Type;
                existing.UnitPriceAmount = dto.UnitPrice?.Amount ?? "0";
                existing.UnitPriceCurrency = dto.UnitPrice?.CurrencyCode ?? "USD";
                existing.BillingCycleInterval = dto.BillingCycle?.Interval;
                existing.BillingCycleFrequency = dto.BillingCycle?.Frequency;
                existing.TrialPeriodInterval = dto.TrialPeriod?.Interval;
                existing.TrialPeriodFrequency = dto.TrialPeriod?.Frequency;
                existing.TaxMode = dto.TaxMode;
                existing.Status = dto.Status ?? "active";
                existing.CustomData = customData;
                existing.PaddleCreatedAt = dto.CreatedAt;
                existing.PaddleUpdatedAt = dto.UpdatedAt;
                existing.LastSyncedAt = now;
            }
            else
            {
                product.Prices.Add(new PaddlePrice
                {
                    PaddlePriceId = dto.Id,
                    PaddleProductId = product.PaddleProductId,
                    Description = dto.Description,
                    Name = dto.Name,
                    Type = dto.Type,
                    UnitPriceAmount = dto.UnitPrice?.Amount ?? "0",
                    UnitPriceCurrency = dto.UnitPrice?.CurrencyCode ?? "USD",
                    BillingCycleInterval = dto.BillingCycle?.Interval,
                    BillingCycleFrequency = dto.BillingCycle?.Frequency,
                    TrialPeriodInterval = dto.TrialPeriod?.Interval,
                    TrialPeriodFrequency = dto.TrialPeriod?.Frequency,
                    TaxMode = dto.TaxMode,
                    Status = dto.Status ?? "active",
                    CustomData = customData,
                    PaddleCreatedAt = dto.CreatedAt,
                    PaddleUpdatedAt = dto.UpdatedAt,
                    LastSyncedAt = now,
                });
            }
        }
    }
}
