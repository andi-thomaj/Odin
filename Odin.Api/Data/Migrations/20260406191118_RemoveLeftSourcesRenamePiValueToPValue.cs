using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Odin.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class RemoveLeftSourcesRenamePiValueToPValue : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LeftSources",
                table: "qpadm_result_era_groups");

            migrationBuilder.RenameColumn(
                name: "PiValue",
                table: "qpadm_result_era_groups",
                newName: "PValue");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "PValue",
                table: "qpadm_result_era_groups",
                newName: "PiValue");

            migrationBuilder.AddColumn<string>(
                name: "LeftSources",
                table: "qpadm_result_era_groups",
                type: "text",
                nullable: false,
                defaultValue: "");
        }
    }
}
