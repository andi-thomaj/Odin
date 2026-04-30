using System.Text.Json;

namespace Odin.Api.Services.Paddle.Models.Customers;

/// <summary>Paddle customer resource (id prefixed <c>ctm_</c>).</summary>
public sealed class PaddleCustomerDto
{
    public string Id { get; set; } = "";
    public string Email { get; set; } = "";
    public string? Name { get; set; }
    public string? MarketingConsent { get; set; }
    public string? Locale { get; set; }
    public string? Status { get; set; }
    public JsonElement? CustomData { get; set; }
    public DateTimeOffset? CreatedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
}

public sealed class PaddleCreateCustomerRequest
{
    public string Email { get; set; } = "";
    public string? Name { get; set; }
    public string? Locale { get; set; }
    public bool? MarketingConsent { get; set; }
    public JsonElement? CustomData { get; set; }
}

public sealed class PaddleUpdateCustomerRequest
{
    public string? Email { get; set; }
    public string? Name { get; set; }
    public string? Locale { get; set; }
    public bool? MarketingConsent { get; set; }
    public string? Status { get; set; }
    public JsonElement? CustomData { get; set; }
}
