using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Odin.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddUserCountry : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Country",
                table: "application_users",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Country",
                table: "application_users");
        }
    }
}
