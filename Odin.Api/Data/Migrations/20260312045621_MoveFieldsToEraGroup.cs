using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Odin.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class MoveFieldsToEraGroup : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DELETE FROM qpadm_result_populations;");

            migrationBuilder.DropForeignKey(
                name: "FK_qpadm_result_populations_qpadm_results_QpadmResultId",
                table: "qpadm_result_populations");

            migrationBuilder.DropColumn(
                name: "LeftSources",
                table: "qpadm_results");

            migrationBuilder.DropColumn(
                name: "PiValue",
                table: "qpadm_results");

            migrationBuilder.DropColumn(
                name: "RightSources",
                table: "qpadm_results");

            migrationBuilder.RenameColumn(
                name: "QpadmResultId",
                table: "qpadm_result_populations",
                newName: "QpadmResultEraGroupId");

            migrationBuilder.CreateTable(
                name: "qpadm_result_era_groups",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    QpadmResultId = table.Column<int>(type: "integer", nullable: false),
                    EraId = table.Column<int>(type: "integer", nullable: false),
                    PiValue = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    RightSources = table.Column<string>(type: "text", nullable: false),
                    LeftSources = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_qpadm_result_era_groups", x => x.Id);
                    table.ForeignKey(
                        name: "FK_qpadm_result_era_groups_eras_EraId",
                        column: x => x.EraId,
                        principalTable: "eras",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_qpadm_result_era_groups_qpadm_results_QpadmResultId",
                        column: x => x.QpadmResultId,
                        principalTable: "qpadm_results",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_qpadm_result_era_groups_EraId",
                table: "qpadm_result_era_groups",
                column: "EraId");

            migrationBuilder.CreateIndex(
                name: "IX_qpadm_result_era_groups_QpadmResultId",
                table: "qpadm_result_era_groups",
                column: "QpadmResultId");

            migrationBuilder.AddForeignKey(
                name: "FK_qpadm_result_populations_qpadm_result_era_groups_QpadmResul~",
                table: "qpadm_result_populations",
                column: "QpadmResultEraGroupId",
                principalTable: "qpadm_result_era_groups",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_qpadm_result_populations_qpadm_result_era_groups_QpadmResul~",
                table: "qpadm_result_populations");

            migrationBuilder.DropTable(
                name: "qpadm_result_era_groups");

            migrationBuilder.RenameColumn(
                name: "QpadmResultEraGroupId",
                table: "qpadm_result_populations",
                newName: "QpadmResultId");

            migrationBuilder.AddColumn<string>(
                name: "LeftSources",
                table: "qpadm_results",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "PiValue",
                table: "qpadm_results",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "RightSources",
                table: "qpadm_results",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddForeignKey(
                name: "FK_qpadm_result_populations_qpadm_results_QpadmResultId",
                table: "qpadm_result_populations",
                column: "QpadmResultId",
                principalTable: "qpadm_results",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
