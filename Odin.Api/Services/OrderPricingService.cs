using Microsoft.EntityFrameworkCore;
using Odin.Api.Data;
using Odin.Api.Data.Entities;
using Odin.Api.Data.Enums;
using Odin.Api.Services.Paddle;

namespace Odin.Api.Services;

/// <summary>
/// Computes order totals entirely from the Paddle-synced catalog: the service's base price
/// comes from the active <see cref="PaddleProduct"/> + <see cref="PaddlePrice"/> tagged with
/// <c>kind = "service"</c>, addon prices come from products tagged <c>kind = "addon"</c>.
/// Discounts (if any) are applied by Paddle at checkout — promo codes are no longer modeled
/// locally; if you need them, sync Paddle Discounts.
/// </summary>
public class OrderPricingService(ApplicationDbContext dbContext) : IOrderPricingService
{
    private const string KindService = "service";
    private const string KindAddon = "addon";

    private static readonly HashSet<string> ExpeditedCodes = new(StringComparer.OrdinalIgnoreCase) { "EXPEDITED" };
    private static readonly HashSet<string> YHaploCodes = new(StringComparer.OrdinalIgnoreCase) { "Y_HAPLOGROUP" };
    private static readonly HashSet<string> MergeCodes = new(StringComparer.OrdinalIgnoreCase) { "MERGE_RAW" };

    public async Task<OrderPricingComputation> ComputeAsync(
        ServiceType service,
        IReadOnlyList<string>? addonPaddleProductIds,
        CancellationToken cancellationToken = default)
    {
        var serviceProduct = await dbContext.PaddleProducts
            .AsNoTracking()
            .Include(p => p.Prices)
            .Where(p => p.Kind == KindService && p.ServiceType == service && p.Status == "active")
            .OrderByDescending(p => p.PaddleUpdatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (serviceProduct is null)
            throw new InvalidOperationException("No active Paddle product is linked to the selected service. Sync products from Paddle first.");

        var servicePrice = SelectActivePrice(serviceProduct)
            ?? throw new InvalidOperationException("The Paddle product for this service has no active price.");
        var basePrice = PaddleMoneyConverter.ToDecimalMajorUnit(servicePrice.UnitPriceAmount, servicePrice.UnitPriceCurrency);

        var requestedIds = (addonPaddleProductIds ?? [])
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        var addonProducts = requestedIds.Count == 0
            ? new List<PaddleProduct>()
            : await dbContext.PaddleProducts
                .AsNoTracking()
                .Include(p => p.Prices)
                .Where(p => p.Kind == KindAddon
                            && p.Status == "active"
                            && requestedIds.Contains(p.PaddleProductId))
                .ToListAsync(cancellationToken);

        if (addonProducts.Count != requestedIds.Count)
            throw new InvalidOperationException("One or more add-ons are invalid or inactive.");

        if (addonProducts.Any(p => p.ParentServiceType != service))
            throw new InvalidOperationException("One or more add-ons are not available for this product.");

        var lines = new List<PricedAddonLine>(addonProducts.Count);
        foreach (var product in addonProducts)
        {
            var price = SelectActivePrice(product)
                ?? throw new InvalidOperationException($"Add-on '{product.Name}' has no active Paddle price.");

            lines.Add(new PricedAddonLine
            {
                PaddleProductId = product.PaddleProductId,
                AddonCode = product.AddonCode ?? string.Empty,
                DisplayName = product.Name,
                UnitPrice = PaddleMoneyConverter.ToDecimalMajorUnit(price.UnitPriceAmount, price.UnitPriceCurrency),
            });
        }

        var addonsSubtotal = lines.Sum(l => l.UnitPrice);
        var subtotal = basePrice + addonsSubtotal;
        var codes = lines.Select(l => l.AddonCode).Where(c => !string.IsNullOrEmpty(c)).ToHashSet(StringComparer.OrdinalIgnoreCase);

        return new OrderPricingComputation
        {
            BasePrice = basePrice,
            AddonsSubtotal = addonsSubtotal,
            SubtotalBeforeDiscount = subtotal,
            DiscountAmount = 0m,
            Total = subtotal,
            AddonLines = lines,
            ExpeditedProcessing = codes.Any(c => ExpeditedCodes.Contains(c)),
            IncludesYHaplogroup = codes.Any(c => YHaploCodes.Contains(c)),
            IncludesRawMerge = codes.Any(c => MergeCodes.Contains(c)),
        };
    }

    private static PaddlePrice? SelectActivePrice(PaddleProduct product) =>
        product.Prices
            .Where(p => p.Status == "active")
            .OrderBy(p => p.Id)
            .FirstOrDefault()
        ?? product.Prices.OrderBy(p => p.Id).FirstOrDefault();
}
