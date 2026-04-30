using System.Text.Json;
using Odin.Api.Services.Paddle.Models.Common;

namespace Odin.Api.Services.Paddle.Models.Prices;

/// <summary>Paddle price resource (id prefixed <c>pri_</c>). Belongs to a product.</summary>
public sealed class PaddlePriceDto
{
    public string Id { get; set; } = "";
    public string ProductId { get; set; } = "";
    public string? Description { get; set; }
    public string? Name { get; set; }
    public string? Type { get; set; }

    public PaddleMoney? UnitPrice { get; set; }
    public List<PaddleMoneyOverride>? UnitPriceOverrides { get; set; }

    public PaddleBillingCycle? BillingCycle { get; set; }
    public PaddleBillingCycle? TrialPeriod { get; set; }
    public PaddleQuantity? Quantity { get; set; }
    public string? TaxMode { get; set; }
    public string? Status { get; set; }
    public JsonElement? CustomData { get; set; }
    public DateTimeOffset? CreatedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
    public PaddleImportMeta? ImportMeta { get; set; }
}

public sealed class PaddleBillingCycle
{
    public string Interval { get; set; } = "";
    public int Frequency { get; set; }
}

public sealed class PaddleQuantity
{
    public int Minimum { get; set; }
    public int Maximum { get; set; }
}

public sealed class PaddleMoneyOverride
{
    public List<string> CountryCodes { get; set; } = [];
    public PaddleMoney UnitPrice { get; set; } = new();
}
