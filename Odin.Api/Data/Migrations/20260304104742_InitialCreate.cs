using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Odin.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "application_users",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    IdentityId = table.Column<string>(type: "text", nullable: false),
                    Username = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Email = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    FirstName = table.Column<string>(type: "text", nullable: false),
                    MiddleName = table.Column<string>(type: "text", nullable: false),
                    LastName = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_application_users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "eras",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_eras", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "raw_genetic_files",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RawData = table.Column<byte[]>(type: "bytea", nullable: false),
                    FileName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_raw_genetic_files", x => x.Id);
                });

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
                name: "populations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    EraId = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_populations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_populations_eras_EraId",
                        column: x => x.EraId,
                        principalTable: "eras",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "genetic_inspections",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    FirstName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    MiddleName = table.Column<string>(type: "text", nullable: false),
                    LastName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    RawGeneticFileId = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_genetic_inspections", x => x.Id);
                    table.ForeignKey(
                        name: "FK_genetic_inspections_application_users_UserId",
                        column: x => x.UserId,
                        principalTable: "application_users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_genetic_inspections_raw_genetic_files_RawGeneticFileId",
                        column: x => x.RawGeneticFileId,
                        principalTable: "raw_genetic_files",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "sub_populations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    PopulationId = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sub_populations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_sub_populations_populations_PopulationId",
                        column: x => x.PopulationId,
                        principalTable: "populations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ethnicities",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    GeneticInspectionId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ethnicities", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ethnicities_genetic_inspections_GeneticInspectionId",
                        column: x => x.GeneticInspectionId,
                        principalTable: "genetic_inspections",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "qpadm_results",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    GeneticInspectionId = table.Column<int>(type: "integer", nullable: false),
                    Weight = table.Column<decimal>(type: "numeric", nullable: false),
                    StandardError = table.Column<decimal>(type: "numeric", nullable: false),
                    ZScore = table.Column<decimal>(type: "numeric", nullable: false),
                    PiValue = table.Column<decimal>(type: "numeric", nullable: false),
                    RightSources = table.Column<string>(type: "text", nullable: false),
                    LeftSources = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_qpadm_results", x => x.Id);
                    table.ForeignKey(
                        name: "FK_qpadm_results_genetic_inspections_GeneticInspectionId",
                        column: x => x.GeneticInspectionId,
                        principalTable: "genetic_inspections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "vahaduo_results",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    GeneticInspectionId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_vahaduo_results", x => x.Id);
                    table.ForeignKey(
                        name: "FK_vahaduo_results_genetic_inspections_GeneticInspectionId",
                        column: x => x.GeneticInspectionId,
                        principalTable: "genetic_inspections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "regions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    EthnicityId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_regions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_regions_ethnicities_EthnicityId",
                        column: x => x.EthnicityId,
                        principalTable: "ethnicities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "qpadm_result_populations",
                columns: table => new
                {
                    PopulationsId = table.Column<int>(type: "integer", nullable: false),
                    QpadmResultId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_qpadm_result_populations", x => new { x.PopulationsId, x.QpadmResultId });
                    table.ForeignKey(
                        name: "FK_qpadm_result_populations_populations_PopulationsId",
                        column: x => x.PopulationsId,
                        principalTable: "populations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_qpadm_result_populations_qpadm_results_QpadmResultId",
                        column: x => x.QpadmResultId,
                        principalTable: "qpadm_results",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
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

            migrationBuilder.CreateTable(
                name: "genetic_inspection_regions",
                columns: table => new
                {
                    GeneticInspectionId = table.Column<int>(type: "integer", nullable: false),
                    RegionId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_genetic_inspection_regions", x => new { x.GeneticInspectionId, x.RegionId });
                    table.ForeignKey(
                        name: "FK_genetic_inspection_regions_genetic_inspections_GeneticInspe~",
                        column: x => x.GeneticInspectionId,
                        principalTable: "genetic_inspections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_genetic_inspection_regions_regions_RegionId",
                        column: x => x.RegionId,
                        principalTable: "regions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ethnicities_GeneticInspectionId",
                table: "ethnicities",
                column: "GeneticInspectionId");

            migrationBuilder.CreateIndex(
                name: "IX_genetic_inspection_regions_RegionId",
                table: "genetic_inspection_regions",
                column: "RegionId");

            migrationBuilder.CreateIndex(
                name: "IX_genetic_inspections_RawGeneticFileId",
                table: "genetic_inspections",
                column: "RawGeneticFileId");

            migrationBuilder.CreateIndex(
                name: "IX_genetic_inspections_UserId",
                table: "genetic_inspections",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_populations_EraId",
                table: "populations",
                column: "EraId");

            migrationBuilder.CreateIndex(
                name: "IX_qpadm_result_populations_QpadmResultId",
                table: "qpadm_result_populations",
                column: "QpadmResultId");

            migrationBuilder.CreateIndex(
                name: "IX_qpadm_result_research_links_ResearchLinkId",
                table: "qpadm_result_research_links",
                column: "ResearchLinkId");

            migrationBuilder.CreateIndex(
                name: "IX_qpadm_results_GeneticInspectionId",
                table: "qpadm_results",
                column: "GeneticInspectionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_regions_EthnicityId",
                table: "regions",
                column: "EthnicityId");

            migrationBuilder.CreateIndex(
                name: "IX_sub_populations_PopulationId",
                table: "sub_populations",
                column: "PopulationId");

            migrationBuilder.CreateIndex(
                name: "IX_vahaduo_results_GeneticInspectionId",
                table: "vahaduo_results",
                column: "GeneticInspectionId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "genetic_inspection_regions");

            migrationBuilder.DropTable(
                name: "qpadm_result_populations");

            migrationBuilder.DropTable(
                name: "qpadm_result_research_links");

            migrationBuilder.DropTable(
                name: "sub_populations");

            migrationBuilder.DropTable(
                name: "vahaduo_results");

            migrationBuilder.DropTable(
                name: "regions");

            migrationBuilder.DropTable(
                name: "qpadm_results");

            migrationBuilder.DropTable(
                name: "research_links");

            migrationBuilder.DropTable(
                name: "populations");

            migrationBuilder.DropTable(
                name: "ethnicities");

            migrationBuilder.DropTable(
                name: "eras");

            migrationBuilder.DropTable(
                name: "genetic_inspections");

            migrationBuilder.DropTable(
                name: "application_users");

            migrationBuilder.DropTable(
                name: "raw_genetic_files");
        }
    }
}
