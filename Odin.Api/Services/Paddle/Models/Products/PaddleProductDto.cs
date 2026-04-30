using System.Text.Json;
using Odin.Api.Services.Paddle.Models.Common;

namespace Odin.Api.Services.Paddle.Models.Products;

/// <summary>Paddle product resource (id prefixed <c>pro_</c>).</summary>
public sealed class PaddleProductDto
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public string? Type { get; set; }
    public string? TaxCategory { get; set; }
    public string? ImageUrl { get; set; }
    public JsonElement? CustomData { get; set; }
    public string? Status { get; set; }
    public DateTimeOffset? CreatedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
    public PaddleImportMeta? ImportMeta { get; set; }

    /// <summary>Populated when the request includes <c>?include=prices</c>.</summary>
    public List<Prices.PaddlePriceDto>? Prices { get; set; }
}

public sealed class PaddleCreateProductRequest
{
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public string? Type { get; set; }
    public string TaxCategory { get; set; } = "standard";
    public string? ImageUrl { get; set; }
    public JsonElement? CustomData { get; set; }
}

public sealed class PaddleUpdateProductRequest
{
    public string? Name { get; set; }
    public string? Description { get; set; }
    public string? Type { get; set; }
    public string? TaxCategory { get; set; }
    public string? ImageUrl { get; set; }
    public JsonElement? CustomData { get; set; }
    public string? Status { get; set; }
}
