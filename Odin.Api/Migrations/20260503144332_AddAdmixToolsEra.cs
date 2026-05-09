using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Odin.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddAdmixToolsEra : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "admix_tools_eras",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_admix_tools_eras", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "admix_tools_eras",
                columns: new[] { "Id", "Name" },
                values: new object[,]
                {
                    { 1, "Ancient" },
                    { 2, "Modern" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_admix_tools_eras_Name",
                table: "admix_tools_eras",
                column: "Name",
                unique: true);

            migrationBuilder.AddColumn<int>(
                name: "AdmixToolsEraId",
                table: "calculators",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.CreateIndex(
                name: "IX_calculators_AdmixToolsEraId",
                table: "calculators",
                column: "AdmixToolsEraId");

            migrationBuilder.AddForeignKey(
                name: "FK_calculators_admix_tools_eras_AdmixToolsEraId",
                table: "calculators",
                column: "AdmixToolsEraId",
                principalTable: "admix_tools_eras",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_calculators_admix_tools_eras_AdmixToolsEraId",
                table: "calculators");

            migrationBuilder.DropTable(
                name: "admix_tools_eras");

            migrationBuilder.DropIndex(
                name: "IX_calculators_AdmixToolsEraId",
                table: "calculators");

            migrationBuilder.DropColumn(
                name: "AdmixToolsEraId",
                table: "calculators");
        }
    }
}
