using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Odin.Api.Migrations
{
    /// <inheritdoc />
    public partial class RenameToQpadmAddG25GeneticInspection : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_genetic_inspection_regions_genetic_inspections_GeneticInspe~",
                table: "genetic_inspection_regions");

            migrationBuilder.DropForeignKey(
                name: "FK_genetic_inspection_regions_regions_RegionId",
                table: "genetic_inspection_regions");

            migrationBuilder.DropForeignKey(
                name: "FK_qpadm_results_genetic_inspections_GeneticInspectionId",
                table: "qpadm_results");

            migrationBuilder.DropTable(
                name: "genetic_inspection_g25_ethnicities");

            migrationBuilder.DropColumn(
                name: "PaternalHaplogroup",
                table: "genetic_inspections");

            migrationBuilder.DropColumn(
                name: "G25Coordinates",
                table: "genetic_inspections");

            migrationBuilder.RenameTable(
                name: "genetic_inspections",
                newName: "qpadm_genetic_inspections");

            migrationBuilder.Sql(@"ALTER INDEX ""PK_genetic_inspections"" RENAME TO ""PK_qpadm_genetic_inspections"";");

            migrationBuilder.RenameIndex(
                name: "IX_genetic_inspections_OrderId",
                table: "qpadm_genetic_inspections",
                newName: "IX_qpadm_genetic_inspections_OrderId");

            migrationBuilder.RenameIndex(
                name: "IX_genetic_inspections_RawGeneticFileId",
                table: "qpadm_genetic_inspections",
                newName: "IX_qpadm_genetic_inspections_RawGeneticFileId");

            migrationBuilder.RenameIndex(
                name: "IX_genetic_inspections_UserId",
                table: "qpadm_genetic_inspections",
                newName: "IX_qpadm_genetic_inspections_UserId");

            migrationBuilder.Sql(@"ALTER TABLE ""qpadm_genetic_inspections"" RENAME CONSTRAINT ""FK_genetic_inspections_application_users_UserId"" TO ""FK_qpadm_genetic_inspections_application_users_UserId"";");
            migrationBuilder.Sql(@"ALTER TABLE ""qpadm_genetic_inspections"" RENAME CONSTRAINT ""FK_genetic_inspections_orders_OrderId"" TO ""FK_qpadm_genetic_inspections_orders_OrderId"";");
            migrationBuilder.Sql(@"ALTER TABLE ""qpadm_genetic_inspections"" RENAME CONSTRAINT ""FK_genetic_inspections_raw_genetic_files_RawGeneticFileId"" TO ""FK_qpadm_genetic_inspections_raw_genetic_files_RawGeneticFileId"";");

            migrationBuilder.RenameTable(
                name: "regions",
                newName: "qpadm_regions");

            migrationBuilder.Sql(@"ALTER INDEX ""PK_regions"" RENAME TO ""PK_qpadm_regions"";");

            migrationBuilder.RenameIndex(
                name: "IX_regions_EthnicityId",
                table: "qpadm_regions",
                newName: "IX_qpadm_regions_EthnicityId");

            migrationBuilder.Sql(@"ALTER TABLE ""qpadm_regions"" RENAME CONSTRAINT ""FK_regions_ethnicities_EthnicityId"" TO ""FK_qpadm_regions_ethnicities_EthnicityId"";");

            migrationBuilder.CreateTable(
                name: "g25_genetic_inspections",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    FirstName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    MiddleName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    LastName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Gender = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    G25Coordinates = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    RawGeneticFileId = table.Column<int>(type: "integer", nullable: false),
                    ProfilePicture = table.Column<byte[]>(type: "bytea", nullable: true),
                    ProfilePictureFileName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    OrderId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_g25_genetic_inspections", x => x.Id);
                    table.ForeignKey(
                        name: "FK_g25_genetic_inspections_application_users_UserId",
                        column: x => x.UserId,
                        principalTable: "application_users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_g25_genetic_inspections_orders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_g25_genetic_inspections_raw_genetic_files_RawGeneticFileId",
                        column: x => x.RawGeneticFileId,
                        principalTable: "raw_genetic_files",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_g25_genetic_inspections_OrderId",
                table: "g25_genetic_inspections",
                column: "OrderId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_g25_genetic_inspections_RawGeneticFileId",
                table: "g25_genetic_inspections",
                column: "RawGeneticFileId");

            migrationBuilder.CreateIndex(
                name: "IX_g25_genetic_inspections_UserId",
                table: "g25_genetic_inspections",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_genetic_inspection_regions_qpadm_genetic_inspections_Geneti~",
                table: "genetic_inspection_regions",
                column: "GeneticInspectionId",
                principalTable: "qpadm_genetic_inspections",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_genetic_inspection_regions_qpadm_regions_RegionId",
                table: "genetic_inspection_regions",
                column: "RegionId",
                principalTable: "qpadm_regions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_qpadm_results_qpadm_genetic_inspections_GeneticInspectionId",
                table: "qpadm_results",
                column: "GeneticInspectionId",
                principalTable: "qpadm_genetic_inspections",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_genetic_inspection_regions_qpadm_genetic_inspections_Geneti~",
                table: "genetic_inspection_regions");

            migrationBuilder.DropForeignKey(
                name: "FK_genetic_inspection_regions_qpadm_regions_RegionId",
                table: "genetic_inspection_regions");

            migrationBuilder.DropForeignKey(
                name: "FK_qpadm_results_qpadm_genetic_inspections_GeneticInspectionId",
                table: "qpadm_results");

            migrationBuilder.DropTable(
                name: "g25_genetic_inspections");

            migrationBuilder.Sql(@"ALTER TABLE ""qpadm_regions"" RENAME CONSTRAINT ""FK_qpadm_regions_ethnicities_EthnicityId"" TO ""FK_regions_ethnicities_EthnicityId"";");

            migrationBuilder.RenameIndex(
                name: "IX_qpadm_regions_EthnicityId",
                table: "qpadm_regions",
                newName: "IX_regions_EthnicityId");

            migrationBuilder.Sql(@"ALTER INDEX ""PK_qpadm_regions"" RENAME TO ""PK_regions"";");

            migrationBuilder.RenameTable(
                name: "qpadm_regions",
                newName: "regions");

            migrationBuilder.Sql(@"ALTER TABLE ""qpadm_genetic_inspections"" RENAME CONSTRAINT ""FK_qpadm_genetic_inspections_application_users_UserId"" TO ""FK_genetic_inspections_application_users_UserId"";");
            migrationBuilder.Sql(@"ALTER TABLE ""qpadm_genetic_inspections"" RENAME CONSTRAINT ""FK_qpadm_genetic_inspections_orders_OrderId"" TO ""FK_genetic_inspections_orders_OrderId"";");
            migrationBuilder.Sql(@"ALTER TABLE ""qpadm_genetic_inspections"" RENAME CONSTRAINT ""FK_qpadm_genetic_inspections_raw_genetic_files_RawGeneticFileId"" TO ""FK_genetic_inspections_raw_genetic_files_RawGeneticFileId"";");

            migrationBuilder.RenameIndex(
                name: "IX_qpadm_genetic_inspections_OrderId",
                table: "qpadm_genetic_inspections",
                newName: "IX_genetic_inspections_OrderId");

            migrationBuilder.RenameIndex(
                name: "IX_qpadm_genetic_inspections_RawGeneticFileId",
                table: "qpadm_genetic_inspections",
                newName: "IX_genetic_inspections_RawGeneticFileId");

            migrationBuilder.RenameIndex(
                name: "IX_qpadm_genetic_inspections_UserId",
                table: "qpadm_genetic_inspections",
                newName: "IX_genetic_inspections_UserId");

            migrationBuilder.Sql(@"ALTER INDEX ""PK_qpadm_genetic_inspections"" RENAME TO ""PK_genetic_inspections"";");

            migrationBuilder.RenameTable(
                name: "qpadm_genetic_inspections",
                newName: "genetic_inspections");

            migrationBuilder.AddColumn<string>(
                name: "G25Coordinates",
                table: "genetic_inspections",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PaternalHaplogroup",
                table: "genetic_inspections",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "genetic_inspection_g25_ethnicities",
                columns: table => new
                {
                    GeneticInspectionId = table.Column<int>(type: "integer", nullable: false),
                    G25EthnicityId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_genetic_inspection_g25_ethnicities", x => new { x.GeneticInspectionId, x.G25EthnicityId });
                    table.ForeignKey(
                        name: "FK_genetic_inspection_g25_ethnicities_g25_ethnicities_G25Ethni~",
                        column: x => x.G25EthnicityId,
                        principalTable: "g25_ethnicities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_genetic_inspection_g25_ethnicities_genetic_inspections_Gene~",
                        column: x => x.GeneticInspectionId,
                        principalTable: "genetic_inspections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_genetic_inspection_g25_ethnicities_G25EthnicityId",
                table: "genetic_inspection_g25_ethnicities",
                column: "G25EthnicityId");

            migrationBuilder.AddForeignKey(
                name: "FK_genetic_inspection_regions_genetic_inspections_GeneticInspe~",
                table: "genetic_inspection_regions",
                column: "GeneticInspectionId",
                principalTable: "genetic_inspections",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_genetic_inspection_regions_regions_RegionId",
                table: "genetic_inspection_regions",
                column: "RegionId",
                principalTable: "regions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_qpadm_results_genetic_inspections_GeneticInspectionId",
                table: "qpadm_results",
                column: "GeneticInspectionId",
                principalTable: "genetic_inspections",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
