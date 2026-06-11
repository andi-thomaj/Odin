using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Odin.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddQpadmPopulationPanelSampleLinks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "qpadm_population_panel_samples",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    QpadmPopulationId = table.Column<int>(type: "integer", nullable: false),
                    Panel = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    SampleId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_qpadm_population_panel_samples", x => x.Id);
                    table.ForeignKey(
                        name: "FK_qpadm_population_panel_samples_qpadm_populations_QpadmPopul~",
                        column: x => x.QpadmPopulationId,
                        principalTable: "qpadm_populations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_qpadm_population_panel_samples_Panel_SampleId",
                table: "qpadm_population_panel_samples",
                columns: new[] { "Panel", "SampleId" });

            migrationBuilder.CreateIndex(
                name: "IX_qpadm_population_panel_samples_QpadmPopulationId_Panel_Samp~",
                table: "qpadm_population_panel_samples",
                columns: new[] { "QpadmPopulationId", "Panel", "SampleId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "qpadm_population_panel_samples");
        }
    }
}
