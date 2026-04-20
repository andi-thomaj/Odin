using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Odin.Api.Migrations
{
    /// <inheritdoc />
    public partial class DropResearchLinks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "qpadm_result_research_links");

            migrationBuilder.DropTable(
                name: "research_links");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "research_links",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_research_links", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "qpadm_result_research_links",
                columns: table => new
                {
                    QpadmResultId = table.Column<int>(type: "integer", nullable: false),
                    ResearchLinkId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_qpadm_result_research_links", x => new { x.QpadmResultId, x.ResearchLinkId });
                    table.ForeignKey(
                        name: "FK_qpadm_result_research_links_qpadm_results_QpadmResultId",
                        column: x => x.QpadmResultId,
                        principalTable: "qpadm_results",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_qpadm_result_research_links_research_links_ResearchLinkId",
                        column: x => x.ResearchLinkId,
                        principalTable: "research_links",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_qpadm_result_research_links_ResearchLinkId",
                table: "qpadm_result_research_links",
                column: "ResearchLinkId");
        }
    }
}
