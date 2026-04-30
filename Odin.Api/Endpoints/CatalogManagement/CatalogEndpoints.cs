using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Odin.Api.Data;
using Odin.Api.Data.Entities;
using Odin.Api.Data.Enums;
using Odin.Api.Endpoints.CatalogManagement.Models;
using Odin.Api.Extensions;
using Odin.Api.Services;
using Odin.Api.Services.Paddle;

namespace Odin.Api.Endpoints.CatalogManagement;

public static class CatalogEndpoints
{
    private const string KindService = "service";
    private const string KindAddon = "addon";

    public static void MapCatalogEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("api/catalog");

        group.MapGet("/products", GetProducts)
            .RequireAuthorization("EmailVerified")
            .RequireRateLimiting("authenticated")
            .WithTags("Catalog")
            .WithSummary("Public catalog of services + their addons, sourced from synced Paddle products.");

        group.MapPost("/preview-price", PreviewPrice)
            .RequireAuthorization("EmailVerified")
            .RequireRateLimiting("authenticated")
            .WithTags("Catalog");
    }

    private static async Task<IResult> GetProducts(ApplicationDbContext db, CancellationToken cancellationToken)
    {
        var products = await db.PaddleProducts
            .AsNoTracking()
            .Include(p => p.Prices)
            .Where(p => p.Status == "active"
                        && (p.Kind == KindService || p.Kind == KindAddon))
            .ToListAsync(cancellationToken);

        var services = products
            .Where(p => p.Kind == KindService && p.ServiceType is not null)
            .OrderBy(p => p.ServiceType)
            .ToList();

        var addonsByParent = products
            .Where(p => p.Kind == KindAddon && p.ParentServiceType is not null)
            .GroupBy(p => p.ParentServiceType!.Value)
            .ToDictionary(g => g.Key, g => g.OrderBy(p => p.Name).ToList());

        var response = new List<GetCatalogProductContract.ProductResponse>(services.Count);

        foreach (var product in services)
        {
            var price = SelectActivePrice(product);
            if (price is null) continue;

            var serviceType = product.ServiceType!.Value;
            var addonRows = addonsByParent.TryGetValue(serviceType, out var addons) ? addons : [];

            response.Add(new GetCatalogProductContract.ProductResponse
            {
                ServiceType = serviceType.ToString(),
                PaddleProductId = product.PaddleProductId,
                PaddlePriceId = price.PaddlePriceId,
                DisplayName = product.Name,
                Description = product.Description,
                ImageUrl = product.ImageUrl,
                BasePrice = PaddleMoneyConverter.ToDecimalMajorUnit(price.UnitPriceAmount, price.UnitPriceCurrency),
                Currency = price.UnitPriceCurrency,
                Addons = addonRows
                    .Select(a => MapAddon(a, SelectActivePrice(a)))
                    .Where(a => a is not null)
                    .Select(a => a!)
                    .ToList(),
            });
        }

        return Results.Ok(response);
    }

    private static async Task<IResult> PreviewPrice(
        IOrderPricingService pricingService,
        [FromBody] PreviewOrderPriceContract.Request request)
    {
        var validationProblem = request.ValidateAndGetProblem();
        if (validationProblem is not null)
            return validationProblem;

        try
        {
            var computation = await pricingService.ComputeAsync(request.Service, request.AddonPaddleProductIds);

            return Results.Ok(new PreviewOrderPriceContract.Response
            {
                BasePrice = computation.BasePrice,
                AddonLines = computation.AddonLines
                    .Select(l => new PreviewOrderPriceContract.AddonLineResponse
                    {
                        PaddleProductId = l.PaddleProductId,
                        AddonCode = l.AddonCode,
                        DisplayName = l.DisplayName,
                        UnitPrice = l.UnitPrice,
                    })
                    .ToList(),
                SubtotalBeforeDiscount = computation.SubtotalBeforeDiscount,
                DiscountAmount = computation.DiscountAmount,
                Total = computation.Total,
            });
        }
        catch (InvalidOperationException ex)
        {
            return Results.BadRequest(new { Message = ex.Message });
        }
    }

    private static GetCatalogProductContract.AddonResponse? MapAddon(PaddleProduct product, PaddlePrice? price)
    {
        if (price is null) return null;
        return new GetCatalogProductContract.AddonResponse
        {
            PaddleProductId = product.PaddleProductId,
            PaddlePriceId = price.PaddlePriceId,
            Code = product.AddonCode ?? string.Empty,
            DisplayName = product.Name,
            Description = product.Description,
            Price = PaddleMoneyConverter.ToDecimalMajorUnit(price.UnitPriceAmount, price.UnitPriceCurrency),
            Currency = price.UnitPriceCurrency,
        };
    }

    private static PaddlePrice? SelectActivePrice(PaddleProduct product) =>
        product.Prices
            .Where(p => p.Status == "active")
            .OrderBy(p => p.Id)
            .FirstOrDefault()
        ?? product.Prices.OrderBy(p => p.Id).FirstOrDefault();
}
