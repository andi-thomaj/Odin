using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Odin.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPopulationPercentage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_qpadm_result_populations_populations_PopulationsId",
                table: "qpadm_result_populations");

            migrationBuilder.DropPrimaryKey(
                name: "PK_qpadm_result_populations",
                table: "qpadm_result_populations");

            migrationBuilder.DropIndex(
                name: "IX_qpadm_result_populations_QpadmResultId",
                table: "qpadm_result_populations");

            migrationBuilder.RenameColumn(
                name: "PopulationsId",
                table: "qpadm_result_populations",
                newName: "PopulationId");

            migrationBuilder.AddColumn<decimal>(
                name: "Percentage",
                table: "qpadm_result_populations",
                type: "numeric(5,2)",
                precision: 5,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddPrimaryKey(
                name: "PK_qpadm_result_populations",
                table: "qpadm_result_populations",
                columns: new[] { "QpadmResultId", "PopulationId" });

            migrationBuilder.CreateIndex(
                name: "IX_qpadm_result_populations_PopulationId",
                table: "qpadm_result_populations",
                column: "PopulationId");

            migrationBuilder.AddForeignKey(
                name: "FK_qpadm_result_populations_populations_PopulationId",
                table: "qpadm_result_populations",
                column: "PopulationId",
                principalTable: "populations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_qpadm_result_populations_populations_PopulationId",
                table: "qpadm_result_populations");

            migrationBuilder.DropPrimaryKey(
                name: "PK_qpadm_result_populations",
                table: "qpadm_result_populations");

            migrationBuilder.DropIndex(
                name: "IX_qpadm_result_populations_PopulationId",
                table: "qpadm_result_populations");

            migrationBuilder.DropColumn(
                name: "Percentage",
                table: "qpadm_result_populations");

            migrationBuilder.RenameColumn(
                name: "PopulationId",
                table: "qpadm_result_populations",
                newName: "PopulationsId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_qpadm_result_populations",
                table: "qpadm_result_populations",
                columns: new[] { "PopulationsId", "QpadmResultId" });

            migrationBuilder.CreateIndex(
                name: "IX_qpadm_result_populations_QpadmResultId",
                table: "qpadm_result_populations",
                column: "QpadmResultId");

            migrationBuilder.AddForeignKey(
                name: "FK_qpadm_result_populations_populations_PopulationsId",
                table: "qpadm_result_populations",
                column: "PopulationsId",
                principalTable: "populations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
