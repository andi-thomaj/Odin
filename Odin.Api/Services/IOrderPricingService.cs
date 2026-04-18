using Odin.Api.Data.Enums;

namespace Odin.Api.Services;

public sealed class OrderPricingComputation
{
    public decimal BasePrice { get; init; }
    public decimal AddonsSubtotal { get; init; }
    public decimal SubtotalBeforeDiscount { get; init; }
    public decimal DiscountAmount { get; init; }
    public decimal Total { get; init; }
    public int? PromoCodeId { get; init; }
    public IReadOnlyList<PricedAddonLine> AddonLines { get; init; } = [];
    public bool ExpeditedProcessing { get; init; }
    public bool IncludesYHaplogroup { get; init; }
    public bool IncludesRawMerge { get; init; }
}

public sealed class PricedAddonLine
{
    public required int ProductAddonId { get; init; }
    public required string Code { get; init; }
    public required string DisplayName { get; init; }
    public decimal UnitPrice { get; init; }
}

public interface IOrderPricingService
{
    Task<OrderPricingComputation> ComputeAsync(
        ServiceType service,
        IReadOnlyList<int>? addonIds,
        string? promoCode,
        CancellationToken cancellationToken = default);
}
