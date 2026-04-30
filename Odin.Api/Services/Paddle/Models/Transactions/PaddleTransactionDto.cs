using System.Text.Json;
using Odin.Api.Services.Paddle.Models.Common;
using Odin.Api.Services.Paddle.Models.Prices;

namespace Odin.Api.Services.Paddle.Models.Transactions;

/// <summary>Paddle transaction resource (id prefixed <c>txn_</c>).</summary>
public sealed class PaddleTransactionDto
{
    public string Id { get; set; } = "";
    public string Status { get; set; } = "";
    public string? CustomerId { get; set; }
    public string? AddressId { get; set; }
    public string? BusinessId { get; set; }
    public string? SubscriptionId { get; set; }
    public string? InvoiceId { get; set; }
    public string? InvoiceNumber { get; set; }
    public string? Origin { get; set; }
    public string? CollectionMode { get; set; }
    public string? DiscountId { get; set; }
    public string CurrencyCode { get; set; } = "";
    public DateTimeOffset? CreatedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
    public DateTimeOffset? BilledAt { get; set; }

    public List<PaddleTransactionItemDto> Items { get; set; } = [];
    public PaddleTransactionDetailsDto? Details { get; set; }
    public List<PaddleTransactionPaymentDto>? Payments { get; set; }
    public PaddleTransactionCheckoutDto? Checkout { get; set; }
    public JsonElement? CustomData { get; set; }
}

public sealed class PaddleTransactionItemDto
{
    public string PriceId { get; set; } = "";
    public PaddlePriceDto? Price { get; set; }
    public int Quantity { get; set; }
    public string? Proration { get; set; }
}

public sealed class PaddleTransactionDetailsDto
{
    public PaddleTransactionTotals? Totals { get; set; }
    public List<PaddleTransactionLineItem>? LineItems { get; set; }
    public PaddleTransactionPayoutTotals? PayoutTotals { get; set; }
}

public sealed class PaddleTransactionTotals
{
    public string Subtotal { get; set; } = "0";
    public string Discount { get; set; } = "0";
    public string Tax { get; set; } = "0";
    public string Total { get; set; } = "0";
    public string Credit { get; set; } = "0";
    public string Balance { get; set; } = "0";
    public string GrandTotal { get; set; } = "0";
    public string? Fee { get; set; }
    public string? Earnings { get; set; }
    public string CurrencyCode { get; set; } = "";
}

public sealed class PaddleTransactionLineItem
{
    public string Id { get; set; } = "";
    public string PriceId { get; set; } = "";
    public int Quantity { get; set; }

    /// <summary>Paddle reports per-unit pricing on transaction line items as <c>unit_totals.subtotal</c> (smallest currency unit, before tax/discount).</summary>
    public PaddleTransactionTotals? UnitTotals { get; set; }

    /// <summary>Per-line totals (subtotal × quantity, plus tax/discount).</summary>
    public PaddleTransactionTotals? Totals { get; set; }
}

public sealed class PaddleTransactionPayoutTotals
{
    public string Subtotal { get; set; } = "0";
    public string Discount { get; set; } = "0";
    public string Tax { get; set; } = "0";
    public string Total { get; set; } = "0";
    public string Fee { get; set; } = "0";
    public string Earnings { get; set; } = "0";
    public string CurrencyCode { get; set; } = "";
}

public sealed class PaddleTransactionPaymentDto
{
    public string PaymentAttemptId { get; set; } = "";
    public string? StoredPaymentMethodId { get; set; }
    public string Amount { get; set; } = "0";
    public string Status { get; set; } = "";
    public string? ErrorCode { get; set; }
    public DateTimeOffset? CapturedAt { get; set; }
    public DateTimeOffset? CreatedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
}

public sealed class PaddleTransactionCheckoutDto
{
    public string? Url { get; set; }
}
