using Odin.Api.Data.Enums;

namespace Odin.Api.Services;

public sealed class OrderPricingComputation
{
    public decimal BasePrice { get; init; }
    public decimal AddonsSubtotal { get; init; }
    public decimal SubtotalBeforeDiscount { get; init; }
    public decimal DiscountAmount { get; init; }
    public decimal Total { get; init; }
    public IReadOnlyList<PricedAddonLine> AddonLines { get; init; } = [];
    public bool ExpeditedProcessing { get; init; }
    public bool IncludesYHaplogroup { get; init; }
    public bool IncludesRawMerge { get; init; }
}

public sealed class PricedAddonLine
{
    /// <summary>Paddle product id for the addon (prefixed <c>pro_</c>).</summary>
    public required string PaddleProductId { get; init; }

    /// <summary>Stable code from <c>custom_data.addon_code</c> (e.g. <c>EXPEDITED</c>). Used by fulfillment-flag detection.</summary>
    public required string AddonCode { get; init; }

    public required string DisplayName { get; init; }
    public decimal UnitPrice { get; init; }
}

public interface IOrderPricingService
{
    Task<OrderPricingComputation> ComputeAsync(
        ServiceType service,
        IReadOnlyList<string>? addonPaddleProductIds,
        CancellationToken cancellationToken = default);
}
