using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Odin.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class RemoveEthnicityGeneticInspectionFk : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ethnicities_genetic_inspections_GeneticInspectionId",
                table: "ethnicities");

            migrationBuilder.DropIndex(
                name: "IX_ethnicities_GeneticInspectionId",
                table: "ethnicities");

            migrationBuilder.DropColumn(
                name: "GeneticInspectionId",
                table: "ethnicities");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "GeneticInspectionId",
                table: "ethnicities",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ethnicities_GeneticInspectionId",
                table: "ethnicities",
                column: "GeneticInspectionId");

            migrationBuilder.AddForeignKey(
                name: "FK_ethnicities_genetic_inspections_GeneticInspectionId",
                table: "ethnicities",
                column: "GeneticInspectionId",
                principalTable: "genetic_inspections",
                principalColumn: "Id");
        }
    }
}
