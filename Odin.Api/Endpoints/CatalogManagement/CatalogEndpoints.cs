using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Odin.Api.Data;
using Odin.Api.Endpoints.CatalogManagement.Models;
using Odin.Api.Extensions;
using Odin.Api.Services;

namespace Odin.Api.Endpoints.CatalogManagement;

public static class CatalogEndpoints
{
    public static void MapCatalogEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("api/catalog");

        group.MapGet("/products", GetProducts)
            .RequireAuthorization("Authenticated")
            .RequireRateLimiting("authenticated");

        group.MapPost("/preview-price", PreviewPrice)
            .RequireAuthorization("Authenticated")
            .RequireRateLimiting("authenticated");
    }

    private static async Task<IResult> GetProducts(ApplicationDbContext db)
    {
        var products = await db.CatalogProducts
            .AsNoTracking()
            .Where(p => p.IsActive)
            .Include(p => p.CatalogProductAddons)
            .ThenInclude(c => c.ProductAddon)
            .OrderBy(p => p.Id)
            .ToListAsync();

        var response = products.Select(p => new GetCatalogProductContract.ProductResponse
        {
            ServiceType = p.ServiceType.ToString(),
            DisplayName = p.DisplayName,
            Description = p.Description,
            BasePrice = p.BasePrice,
            Addons = p.CatalogProductAddons
                .Where(c => c.ProductAddon.IsActive)
                .OrderBy(c => c.ProductAddon.DisplayName)
                .Select(c => new GetCatalogProductContract.AddonResponse
                {
                    Id = c.ProductAddon.Id,
                    Code = c.ProductAddon.Code,
                    DisplayName = c.ProductAddon.DisplayName,
                    Price = c.ProductAddon.Price
                })
                .ToList()
        }).ToList();

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
            var computation = await pricingService.ComputeAsync(request.Service, request.AddonIds, request.PromoCode);

            return Results.Ok(new PreviewOrderPriceContract.Response
            {
                BasePrice = computation.BasePrice,
                AddonLines = computation.AddonLines
                    .Select(l => new PreviewOrderPriceContract.AddonLineResponse
                    {
                        ProductAddonId = l.ProductAddonId,
                        Code = l.Code,
                        DisplayName = l.DisplayName,
                        UnitPrice = l.UnitPrice
                    })
                    .ToList(),
                SubtotalBeforeDiscount = computation.SubtotalBeforeDiscount,
                DiscountAmount = computation.DiscountAmount,
                Total = computation.Total
            });
        }
        catch (InvalidOperationException ex)
        {
            return Results.BadRequest(new { Message = ex.Message });
        }
    }
}
