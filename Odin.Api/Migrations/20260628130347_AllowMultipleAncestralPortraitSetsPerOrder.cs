using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Odin.Api.Migrations
{
    /// <inheritdoc />
    public partial class AllowMultipleAncestralPortraitSetsPerOrder : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ancestral_portrait_sets_OrderId",
                table: "ancestral_portrait_sets");

            migrationBuilder.CreateIndex(
                name: "IX_ancestral_portrait_sets_OrderId",
                table: "ancestral_portrait_sets",
                column: "OrderId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ancestral_portrait_sets_OrderId",
                table: "ancestral_portrait_sets");

            migrationBuilder.CreateIndex(
                name: "IX_ancestral_portrait_sets_OrderId",
                table: "ancestral_portrait_sets",
                column: "OrderId",
                unique: true);
        }
    }
}
