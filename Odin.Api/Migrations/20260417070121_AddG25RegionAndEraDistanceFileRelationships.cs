using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Odin.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddG25RegionAndEraDistanceFileRelationships : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "g25_regions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_g25_regions", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_g25_regions_Name",
                table: "g25_regions",
                column: "Name",
                unique: true);

            migrationBuilder.Sql(@"
                INSERT INTO g25_regions (""Name"", ""CreatedAt"", ""CreatedBy"", ""UpdatedAt"", ""UpdatedBy"")
                SELECT 'Default', NOW() AT TIME ZONE 'UTC', 'migration', NOW() AT TIME ZONE 'UTC', ''
                WHERE EXISTS (SELECT 1 FROM g25_ethnicities)
                  AND NOT EXISTS (SELECT 1 FROM g25_regions WHERE ""Name"" = 'Default');
            ");

            migrationBuilder.AddColumn<int>(
                name: "G25RegionId",
                table: "g25_ethnicities",
                type: "integer",
                nullable: true);

            migrationBuilder.Sql(@"
                UPDATE g25_ethnicities
                SET ""G25RegionId"" = (SELECT ""Id"" FROM g25_regions WHERE ""Name"" = 'Default' LIMIT 1)
                WHERE ""G25RegionId"" IS NULL;
            ");

            migrationBuilder.AlterColumn<int>(
                name: "G25RegionId",
                table: "g25_ethnicities",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_g25_ethnicities_G25RegionId",
                table: "g25_ethnicities",
                column: "G25RegionId");

            migrationBuilder.AddForeignKey(
                name: "FK_g25_ethnicities_g25_regions_G25RegionId",
                table: "g25_ethnicities",
                column: "G25RegionId",
                principalTable: "g25_regions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.DropForeignKey(
                name: "FK_g25_eras_g25_distance_files_G25DistanceFileId",
                table: "g25_eras");

            migrationBuilder.DropIndex(
                name: "IX_g25_eras_G25DistanceFileId",
                table: "g25_eras");

            migrationBuilder.AddColumn<int>(
                name: "G25EraId",
                table: "g25_distance_files",
                type: "integer",
                nullable: true);

            migrationBuilder.Sql(@"
                UPDATE g25_distance_files df
                SET ""G25EraId"" = e.""Id""
                FROM g25_eras e
                WHERE e.""G25DistanceFileId"" = df.""Id"" AND df.""G25EraId"" IS NULL;
            ");

            migrationBuilder.Sql(@"
                INSERT INTO g25_eras (""Name"", ""G25DistanceFileId"", ""CreatedAt"", ""CreatedBy"", ""UpdatedAt"", ""UpdatedBy"")
                SELECT 'Default', 0, NOW() AT TIME ZONE 'UTC', 'migration', NOW() AT TIME ZONE 'UTC', ''
                WHERE EXISTS (SELECT 1 FROM g25_distance_files WHERE ""G25EraId"" IS NULL)
                  AND NOT EXISTS (SELECT 1 FROM g25_eras WHERE ""Name"" = 'Default');
            ");

            migrationBuilder.Sql(@"
                UPDATE g25_distance_files
                SET ""G25EraId"" = (SELECT ""Id"" FROM g25_eras WHERE ""Name"" = 'Default' LIMIT 1)
                WHERE ""G25EraId"" IS NULL;
            ");

            migrationBuilder.DropColumn(
                name: "G25DistanceFileId",
                table: "g25_eras");

            migrationBuilder.AlterColumn<int>(
                name: "G25EraId",
                table: "g25_distance_files",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_g25_distance_files_G25EraId",
                table: "g25_distance_files",
                column: "G25EraId");

            migrationBuilder.AddForeignKey(
                name: "FK_g25_distance_files_g25_eras_G25EraId",
                table: "g25_distance_files",
                column: "G25EraId",
                principalTable: "g25_eras",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_g25_distance_files_g25_eras_G25EraId",
                table: "g25_distance_files");

            migrationBuilder.DropForeignKey(
                name: "FK_g25_ethnicities_g25_regions_G25RegionId",
                table: "g25_ethnicities");

            migrationBuilder.DropTable(
                name: "g25_regions");

            migrationBuilder.DropIndex(
                name: "IX_g25_ethnicities_G25RegionId",
                table: "g25_ethnicities");

            migrationBuilder.DropIndex(
                name: "IX_g25_distance_files_G25EraId",
                table: "g25_distance_files");

            migrationBuilder.DropColumn(
                name: "G25RegionId",
                table: "g25_ethnicities");

            migrationBuilder.DropColumn(
                name: "G25EraId",
                table: "g25_distance_files");

            migrationBuilder.AddColumn<int>(
                name: "G25DistanceFileId",
                table: "g25_eras",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_g25_eras_G25DistanceFileId",
                table: "g25_eras",
                column: "G25DistanceFileId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_g25_eras_g25_distance_files_G25DistanceFileId",
                table: "g25_eras",
                column: "G25DistanceFileId",
                principalTable: "g25_distance_files",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
