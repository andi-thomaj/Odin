using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Odin.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddEmailVerificationToUsers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "EmailVerified",
                table: "application_users",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.Sql(
                """
                ALTER TABLE application_users ALTER COLUMN "EmailVerified" SET DEFAULT false;
                """);

            migrationBuilder.AddColumn<string>(
                name: "EmailVerificationToken",
                table: "application_users",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "EmailVerificationTokenExpiresAt",
                table: "application_users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_application_users_EmailVerificationToken",
                table: "application_users",
                column: "EmailVerificationToken");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_application_users_EmailVerificationToken",
                table: "application_users");

            migrationBuilder.DropColumn(
                name: "EmailVerificationTokenExpiresAt",
                table: "application_users");

            migrationBuilder.DropColumn(
                name: "EmailVerificationToken",
                table: "application_users");

            migrationBuilder.DropColumn(
                name: "EmailVerified",
                table: "application_users");
        }
    }
}
