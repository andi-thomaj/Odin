using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Odin.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AllowSharedRawGeneticFile : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_genetic_inspections_RawGeneticFileId",
                table: "genetic_inspections");

            migrationBuilder.CreateIndex(
                name: "IX_genetic_inspections_RawGeneticFileId",
                table: "genetic_inspections",
                column: "RawGeneticFileId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_genetic_inspections_RawGeneticFileId",
                table: "genetic_inspections");

            migrationBuilder.CreateIndex(
                name: "IX_genetic_inspections_RawGeneticFileId",
                table: "genetic_inspections",
                column: "RawGeneticFileId",
                unique: true);
        }
    }
}
