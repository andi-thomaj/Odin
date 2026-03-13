using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Odin.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddGenderHaplogroupAndMergedData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "FileName",
                table: "raw_genetic_files",
                newName: "RawDataFileName");

            migrationBuilder.AddColumn<byte[]>(
                name: "MergedRawData",
                table: "raw_genetic_files",
                type: "bytea",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MergedRawDataFileName",
                table: "raw_genetic_files",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Gender",
                table: "genetic_inspections",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PaternalHaplogroup",
                table: "genetic_inspections",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MergedRawData",
                table: "raw_genetic_files");

            migrationBuilder.DropColumn(
                name: "MergedRawDataFileName",
                table: "raw_genetic_files");

            migrationBuilder.DropColumn(
                name: "Gender",
                table: "genetic_inspections");

            migrationBuilder.DropColumn(
                name: "PaternalHaplogroup",
                table: "genetic_inspections");

            migrationBuilder.RenameColumn(
                name: "RawDataFileName",
                table: "raw_genetic_files",
                newName: "FileName");
        }
    }
}
