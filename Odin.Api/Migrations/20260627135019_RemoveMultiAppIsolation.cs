using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Odin.Api.Migrations
{
    /// <inheritdoc />
    public partial class RemoveMultiAppIsolation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "applications");

            migrationBuilder.DropIndex(
                name: "IX_raw_genetic_files_App_CreatedBy_RawDataFileName",
                table: "raw_genetic_files");

            migrationBuilder.DropIndex(
                name: "IX_qpadm_orders_App_CreatedBy_CreatedAt",
                table: "qpadm_orders");

            migrationBuilder.DropIndex(
                name: "IX_g25_orders_App_CreatedBy_CreatedAt",
                table: "g25_orders");

            migrationBuilder.DropIndex(
                name: "IX_application_users_IdentityId_App",
                table: "application_users");

            migrationBuilder.DropIndex(
                name: "IX_app_store_transactions_App_TransactionId",
                table: "app_store_transactions");

            migrationBuilder.DropColumn(
                name: "App",
                table: "reports");

            migrationBuilder.DropColumn(
                name: "App",
                table: "raw_genetic_files");

            migrationBuilder.DropColumn(
                name: "App",
                table: "qpadm_results");

            migrationBuilder.DropColumn(
                name: "App",
                table: "qpadm_orders");

            migrationBuilder.DropColumn(
                name: "App",
                table: "qpadm_genetic_inspections");

            migrationBuilder.DropColumn(
                name: "App",
                table: "qpadm_clade_results");

            migrationBuilder.DropColumn(
                name: "App",
                table: "notifications");

            migrationBuilder.DropColumn(
                name: "App",
                table: "g25_target_coordinates");

            migrationBuilder.DropColumn(
                name: "App",
                table: "g25_saved_coordinates");

            migrationBuilder.DropColumn(
                name: "App",
                table: "g25_pca_results");

            migrationBuilder.DropColumn(
                name: "App",
                table: "g25_orders");

            migrationBuilder.DropColumn(
                name: "App",
                table: "g25_genetic_inspections");

            migrationBuilder.DropColumn(
                name: "App",
                table: "g25_distance_results");

            migrationBuilder.DropColumn(
                name: "App",
                table: "g25_admixture_results");

            migrationBuilder.DropColumn(
                name: "App",
                table: "calculators");

            migrationBuilder.DropColumn(
                name: "App",
                table: "application_users");

            migrationBuilder.DropColumn(
                name: "App",
                table: "app_store_transactions");

            migrationBuilder.CreateIndex(
                name: "IX_raw_genetic_files_CreatedBy_RawDataFileName",
                table: "raw_genetic_files",
                columns: new[] { "CreatedBy", "RawDataFileName" },
                unique: true,
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_qpadm_orders_CreatedBy_CreatedAt",
                table: "qpadm_orders",
                columns: new[] { "CreatedBy", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_g25_orders_CreatedBy_CreatedAt",
                table: "g25_orders",
                columns: new[] { "CreatedBy", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_application_users_IdentityId",
                table: "application_users",
                column: "IdentityId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_app_store_transactions_TransactionId",
                table: "app_store_transactions",
                column: "TransactionId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_raw_genetic_files_CreatedBy_RawDataFileName",
                table: "raw_genetic_files");

            migrationBuilder.DropIndex(
                name: "IX_qpadm_orders_CreatedBy_CreatedAt",
                table: "qpadm_orders");

            migrationBuilder.DropIndex(
                name: "IX_g25_orders_CreatedBy_CreatedAt",
                table: "g25_orders");

            migrationBuilder.DropIndex(
                name: "IX_application_users_IdentityId",
                table: "application_users");

            migrationBuilder.DropIndex(
                name: "IX_app_store_transactions_TransactionId",
                table: "app_store_transactions");

            migrationBuilder.AddColumn<string>(
                name: "App",
                table: "reports",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "App",
                table: "raw_genetic_files",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "App",
                table: "qpadm_results",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "App",
                table: "qpadm_orders",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "App",
                table: "qpadm_genetic_inspections",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "App",
                table: "qpadm_clade_results",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "App",
                table: "notifications",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "App",
                table: "g25_target_coordinates",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "App",
                table: "g25_saved_coordinates",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "App",
                table: "g25_pca_results",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "App",
                table: "g25_orders",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "App",
                table: "g25_genetic_inspections",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "App",
                table: "g25_distance_results",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "App",
                table: "g25_admixture_results",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "App",
                table: "calculators",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "App",
                table: "application_users",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "App",
                table: "app_store_transactions",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "applications",
                columns: table => new
                {
                    Key = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    FromEmail = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    FromName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    FrontendBaseUrl = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_applications", x => x.Key);
                });

            migrationBuilder.CreateIndex(
                name: "IX_raw_genetic_files_App_CreatedBy_RawDataFileName",
                table: "raw_genetic_files",
                columns: new[] { "App", "CreatedBy", "RawDataFileName" },
                unique: true,
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_qpadm_orders_App_CreatedBy_CreatedAt",
                table: "qpadm_orders",
                columns: new[] { "App", "CreatedBy", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_g25_orders_App_CreatedBy_CreatedAt",
                table: "g25_orders",
                columns: new[] { "App", "CreatedBy", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_application_users_IdentityId_App",
                table: "application_users",
                columns: new[] { "IdentityId", "App" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_app_store_transactions_App_TransactionId",
                table: "app_store_transactions",
                columns: new[] { "App", "TransactionId" },
                unique: true);
        }
    }
}
