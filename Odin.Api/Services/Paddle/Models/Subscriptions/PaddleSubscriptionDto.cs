using System.Text.Json;
using Odin.Api.Services.Paddle.Models.Common;
using Odin.Api.Services.Paddle.Models.Prices;

namespace Odin.Api.Services.Paddle.Models.Subscriptions;

/// <summary>Paddle subscription resource (id prefixed <c>sub_</c>).</summary>
public sealed class PaddleSubscriptionDto
{
    public string Id { get; set; } = "";
    public string Status { get; set; } = "";
    public string CustomerId { get; set; } = "";
    public string AddressId { get; set; } = "";
    public string? BusinessId { get; set; }
    public string CurrencyCode { get; set; } = "";
    public DateTimeOffset? CreatedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? FirstBilledAt { get; set; }
    public DateTimeOffset? NextBilledAt { get; set; }
    public DateTimeOffset? PausedAt { get; set; }
    public DateTimeOffset? CanceledAt { get; set; }

    public PaddleBillingCycle? BillingCycle { get; set; }
    public PaddleSubscriptionPeriod? CurrentBillingPeriod { get; set; }
    public string? CollectionMode { get; set; }
    public PaddleScheduledChange? ScheduledChange { get; set; }
    public List<PaddleSubscriptionItemDto> Items { get; set; } = [];
    public JsonElement? CustomData { get; set; }
    public string? ManagementUrls { get; set; }
}

public sealed class PaddleSubscriptionItemDto
{
    public string Status { get; set; } = "";
    public int Quantity { get; set; }
    public bool Recurring { get; set; }
    public DateTimeOffset? CreatedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
    public DateTimeOffset? PreviouslyBilledAt { get; set; }
    public DateTimeOffset? NextBilledAt { get; set; }
    public PaddlePriceDto? Price { get; set; }
}

public sealed class PaddleSubscriptionPeriod
{
    public DateTimeOffset StartsAt { get; set; }
    public DateTimeOffset EndsAt { get; set; }
}

public sealed class PaddleScheduledChange
{
    public string Action { get; set; } = "";
    public DateTimeOffset EffectiveAt { get; set; }
    public DateTimeOffset? ResumeAt { get; set; }
}
