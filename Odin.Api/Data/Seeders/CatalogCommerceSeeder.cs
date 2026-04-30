using Microsoft.EntityFrameworkCore;
using Odin.Api.Data.Entities;
using Odin.Api.Data.Enums;

namespace Odin.Api.Data.Seeders;

/// <summary>
/// Seeds the catalog of purchasable services (qpAdm + G25), their addons, and the
/// initial promo code. Composed of two passes — qpAdm catalog (fresh DB) and the
/// G25 catalog product (idempotent on existing DBs).
/// </summary>
internal sealed class CatalogCommerceSeeder(ApplicationDbContext context)
{
    public async Task SeedAsync()
    {
        if (await context.CatalogProducts.AnyAsync())
            return;

        var product = new CatalogProduct
        {
            ServiceType = ServiceType.qpAdm,
            DisplayName = "qpAdm ancestry analysis",
            Description = "Deep ancestry modeling with reference populations.",
            BasePrice = 49.99m,
            IsActive = true
        };
        context.CatalogProducts.Add(product);
        await context.SaveChangesAsync();

        var addons = new[]
        {
            new ProductAddon
            {
                Code = "EXPEDITED",
                DisplayName = "Compute faster your results",
                Price = 20m,
                IsActive = true
            },
            new ProductAddon
            {
                Code = "Y_HAPLOGROUP",
                DisplayName = "Find your Y haplogroup",
                Price = 20m,
                IsActive = true
            },
            new ProductAddon
            {
                Code = "MERGE_RAW",
                DisplayName = "Merge your raw data",
                Price = 40m,
                IsActive = true
            }
        };
        context.ProductAddons.AddRange(addons);
        await context.SaveChangesAsync();

        foreach (var addon in addons)
        {
            context.CatalogProductAddons.Add(new CatalogProductAddon
            {
                CatalogProductId = product.Id,
                ProductAddonId = addon.Id
            });
        }

        context.PromoCodes.Add(new PromoCode
        {
            Code = "WELCOME10",
            DiscountType = PromoDiscountType.Percent,
            Value = 10m,
            IsActive = true,
            ApplicableService = ServiceType.qpAdm,
            RedemptionCount = 0
        });

        await context.SaveChangesAsync();

        if (!await context.CatalogProducts.AnyAsync(p => p.ServiceType == ServiceType.g25))
        {
            var g25Product = new CatalogProduct
            {
                ServiceType = ServiceType.g25,
                DisplayName = "G25 ancestry analysis",
                Description = "G25 coordinate-based distance and admixture analysis.",
                BasePrice = 29.99m,
                IsActive = true
            };
            context.CatalogProducts.Add(g25Product);
            await context.SaveChangesAsync();
        }
    }
}
