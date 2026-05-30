using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Odin.Api.Migrations
{
    /// <summary>
    /// Removes the Paddle integration tables and the <c>qpadm_orders.addons_json</c> column.
    /// Forward-only: rolling this back would require re-introducing the Paddle code paths,
    /// which were removed in the same change.
    /// </summary>
    public partial class DropPaddleTables : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Order matters: drop FK-dependent tables before their parents.
            migrationBuilder.DropTable(name: "paddle_payments");
            migrationBuilder.DropTable(name: "paddle_transactions");
            migrationBuilder.DropTable(name: "paddle_subscriptions");
            migrationBuilder.DropTable(name: "paddle_prices");
            migrationBuilder.DropTable(name: "paddle_notifications");
            migrationBuilder.DropTable(name: "paddle_products");
            migrationBuilder.DropTable(name: "paddle_customers");

            migrationBuilder.DropColumn(
                name: "AddonsJson",
                table: "qpadm_orders");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            throw new System.NotSupportedException(
                "DropPaddleTables is forward-only. Rolling back requires reintroducing the Paddle integration code.");
        }
    }
}
