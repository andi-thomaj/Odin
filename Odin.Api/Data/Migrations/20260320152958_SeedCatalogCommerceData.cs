using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Odin.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class SeedCatalogCommerceData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                INSERT INTO catalog_products ("ServiceType", "DisplayName", "Description", "BasePrice", "IsActive")
                SELECT 'qpAdm', 'qpAdm ancestry analysis', 'Deep ancestry modeling with reference populations.', 49.99, TRUE
                WHERE NOT EXISTS (SELECT 1 FROM catalog_products cp WHERE cp."ServiceType" = 'qpAdm');

                INSERT INTO product_addons ("Code", "DisplayName", "Price", "IsActive")
                SELECT 'EXPEDITED', 'Compute faster your results', 20, TRUE
                WHERE NOT EXISTS (SELECT 1 FROM product_addons pa WHERE pa."Code" = 'EXPEDITED');

                INSERT INTO product_addons ("Code", "DisplayName", "Price", "IsActive")
                SELECT 'Y_HAPLOGROUP', 'Find your Y haplogroup', 20, TRUE
                WHERE NOT EXISTS (SELECT 1 FROM product_addons pa WHERE pa."Code" = 'Y_HAPLOGROUP');

                INSERT INTO product_addons ("Code", "DisplayName", "Price", "IsActive")
                SELECT 'MERGE_RAW', 'Merge your raw data', 40, TRUE
                WHERE NOT EXISTS (SELECT 1 FROM product_addons pa WHERE pa."Code" = 'MERGE_RAW');

                INSERT INTO catalog_product_addons ("CatalogProductId", "ProductAddonId")
                SELECT cp."Id", pa."Id"
                FROM catalog_products cp
                CROSS JOIN product_addons pa
                WHERE cp."ServiceType" = 'qpAdm'
                  AND pa."Code" IN ('EXPEDITED', 'Y_HAPLOGROUP', 'MERGE_RAW')
                  AND NOT EXISTS (
                    SELECT 1 FROM catalog_product_addons cpa
                    WHERE cpa."CatalogProductId" = cp."Id" AND cpa."ProductAddonId" = pa."Id");

                INSERT INTO promo_codes ("Code", "DiscountType", "Value", "RedemptionCount", "IsActive", "ApplicableService")
                SELECT 'WELCOME10', 'Percent', 10, 0, TRUE, 'qpAdm'
                WHERE NOT EXISTS (SELECT 1 FROM promo_codes pc WHERE pc."Code" = 'WELCOME10');

                SELECT setval(
                  pg_get_serial_sequence('catalog_products', 'Id'),
                  COALESCE((SELECT MAX("Id") FROM catalog_products), 1),
                  TRUE);
                SELECT setval(
                  pg_get_serial_sequence('product_addons', 'Id'),
                  COALESCE((SELECT MAX("Id") FROM product_addons), 1),
                  TRUE);
                SELECT setval(
                  pg_get_serial_sequence('promo_codes', 'Id'),
                  COALESCE((SELECT MAX("Id") FROM promo_codes), 1),
                  TRUE);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DELETE FROM catalog_product_addons
                WHERE "CatalogProductId" IN (
                  SELECT "Id" FROM catalog_products WHERE "ServiceType" = 'qpAdm');

                DELETE FROM promo_codes WHERE "Code" = 'WELCOME10';

                DELETE FROM product_addons
                WHERE "Code" IN ('EXPEDITED', 'Y_HAPLOGROUP', 'MERGE_RAW')
                  AND NOT EXISTS (
                    SELECT 1 FROM order_line_addons ola WHERE ola."ProductAddonId" = product_addons."Id");

                DELETE FROM catalog_products WHERE "ServiceType" = 'qpAdm';
                """);
        }
    }
}
