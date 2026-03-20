using Microsoft.EntityFrameworkCore;
using Odin.Api.Data;
using Odin.Api.Data.Entities;
using Odin.Api.Data.Enums;

namespace Odin.Api.Services;

public class OrderPricingService(ApplicationDbContext dbContext) : IOrderPricingService
{
    private static readonly HashSet<string> ExpeditedCodes = new(StringComparer.OrdinalIgnoreCase) { "EXPEDITED" };
    private static readonly HashSet<string> YHaploCodes = new(StringComparer.OrdinalIgnoreCase) { "Y_HAPLOGROUP" };
    private static readonly HashSet<string> MergeCodes = new(StringComparer.OrdinalIgnoreCase) { "MERGE_RAW" };

    public async Task<OrderPricingComputation> ComputeAsync(
        OrderService service,
        IReadOnlyList<int>? addonIds,
        string? promoCode,
        CancellationToken cancellationToken = default)
    {
        var catalog = await dbContext.CatalogProducts
            .AsNoTracking()
            .Include(p => p.CatalogProductAddons)
            .ThenInclude(cpa => cpa.ProductAddon)
            .FirstOrDefaultAsync(p => p.ServiceType == service && p.IsActive, cancellationToken);

        if (catalog is null)
            throw new InvalidOperationException("No active catalog entry for the selected service.");

        var allowedAddonIds = catalog.CatalogProductAddons
            .Where(cpa => cpa.ProductAddon.IsActive)
            .Select(cpa => cpa.ProductAddonId)
            .ToHashSet();

        // Form / JSON binders may leave AddonIds null when no values were sent.
        var distinctRequested = (addonIds ?? []).Distinct().ToList();
        var invalid = distinctRequested.Where(id => !allowedAddonIds.Contains(id)).ToList();
        if (invalid.Count > 0)
            throw new InvalidOperationException("One or more add-ons are not available for this product.");

        var addonEntities = await dbContext.ProductAddons
            .AsNoTracking()
            .Where(a => distinctRequested.Contains(a.Id) && a.IsActive)
            .ToListAsync(cancellationToken);

        if (addonEntities.Count != distinctRequested.Count)
            throw new InvalidOperationException("One or more add-ons are invalid or inactive.");

        var lines = addonEntities
            .Select(a => new PricedAddonLine
            {
                ProductAddonId = a.Id,
                Code = a.Code,
                DisplayName = a.DisplayName,
                UnitPrice = a.Price
            })
            .ToList();

        var addonsSubtotal = lines.Sum(l => l.UnitPrice);
        var subtotal = catalog.BasePrice + addonsSubtotal;

        decimal discount = 0;
        int? promoId = null;
        var normalizedPromo = string.IsNullOrWhiteSpace(promoCode) ? null : promoCode.Trim().ToUpperInvariant();

        if (normalizedPromo is not null)
        {
            var now = DateTime.UtcNow;
            var promo = await dbContext.PromoCodes
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    p => p.Code == normalizedPromo && p.IsActive,
                    cancellationToken);

            if (promo is null)
                throw new InvalidOperationException("Promo code is not valid.");

            if (promo.ValidFromUtc is { } from && now < from)
                throw new InvalidOperationException("Promo code is not yet active.");

            if (promo.ValidUntilUtc is { } until && now > until)
                throw new InvalidOperationException("Promo code has expired.");

            if (promo.MaxRedemptions is { } max && promo.RedemptionCount >= max)
                throw new InvalidOperationException("Promo code has reached its redemption limit.");

            if (promo.ApplicableService is { } onlyFor && onlyFor != service)
                throw new InvalidOperationException("Promo code does not apply to this product.");

            discount = promo.DiscountType switch
            {
                PromoDiscountType.Percent => Math.Round(subtotal * (promo.Value / 100m), 2, MidpointRounding.AwayFromZero),
                PromoDiscountType.FixedAmount => Math.Min(promo.Value, subtotal),
                _ => throw new InvalidOperationException("Unsupported discount type.")
            };

            if (discount < 0)
                discount = 0;

            promoId = promo.Id;
        }

        var total = Math.Round(subtotal - discount, 2, MidpointRounding.AwayFromZero);

        var codes = lines.Select(l => l.Code).ToHashSet(StringComparer.OrdinalIgnoreCase);
        return new OrderPricingComputation
        {
            BasePrice = catalog.BasePrice,
            AddonsSubtotal = addonsSubtotal,
            SubtotalBeforeDiscount = subtotal,
            DiscountAmount = discount,
            Total = total,
            PromoCodeId = promoId,
            AddonLines = lines,
            ExpeditedProcessing = codes.Any(c => ExpeditedCodes.Contains(c)),
            IncludesYHaplogroup = codes.Any(c => YHaploCodes.Contains(c)),
            IncludesRawMerge = codes.Any(c => MergeCodes.Contains(c))
        };
    }
}
