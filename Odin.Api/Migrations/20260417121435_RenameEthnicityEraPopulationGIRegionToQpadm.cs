using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Odin.Api.Migrations
{
    /// <inheritdoc />
    public partial class RenameEthnicityEraPopulationGIRegionToQpadm : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ethnicities -> qpadm_ethnicities
            migrationBuilder.RenameTable(
                name: "ethnicities",
                newName: "qpadm_ethnicities");

            migrationBuilder.Sql(@"ALTER INDEX ""PK_ethnicities"" RENAME TO ""PK_qpadm_ethnicities"";");
            migrationBuilder.Sql(@"ALTER TABLE ""qpadm_regions"" RENAME CONSTRAINT ""FK_qpadm_regions_ethnicities_EthnicityId"" TO ""FK_qpadm_regions_qpadm_ethnicities_EthnicityId"";");

            // eras -> qpadm_eras
            migrationBuilder.RenameTable(
                name: "eras",
                newName: "qpadm_eras");

            migrationBuilder.Sql(@"ALTER INDEX ""PK_eras"" RENAME TO ""PK_qpadm_eras"";");
            migrationBuilder.Sql(@"ALTER TABLE ""qpadm_result_era_groups"" RENAME CONSTRAINT ""FK_qpadm_result_era_groups_eras_EraId"" TO ""FK_qpadm_result_era_groups_qpadm_eras_EraId"";");

            // populations -> qpadm_populations
            migrationBuilder.RenameTable(
                name: "populations",
                newName: "qpadm_populations");

            migrationBuilder.Sql(@"ALTER INDEX ""PK_populations"" RENAME TO ""PK_qpadm_populations"";");

            migrationBuilder.RenameIndex(
                name: "IX_populations_EraId",
                table: "qpadm_populations",
                newName: "IX_qpadm_populations_EraId");

            migrationBuilder.RenameIndex(
                name: "IX_populations_MusicTrackId",
                table: "qpadm_populations",
                newName: "IX_qpadm_populations_MusicTrackId");

            migrationBuilder.Sql(@"ALTER TABLE ""qpadm_populations"" RENAME CONSTRAINT ""FK_populations_eras_EraId"" TO ""FK_qpadm_populations_qpadm_eras_EraId"";");
            migrationBuilder.Sql(@"ALTER TABLE ""qpadm_populations"" RENAME CONSTRAINT ""FK_populations_music_tracks_MusicTrackId"" TO ""FK_qpadm_populations_music_tracks_MusicTrackId"";");
            migrationBuilder.Sql(@"ALTER TABLE ""qpadm_result_populations"" RENAME CONSTRAINT ""FK_qpadm_result_populations_populations_PopulationId"" TO ""FK_qpadm_result_populations_qpadm_populations_PopulationId"";");

            // genetic_inspection_regions -> qpadm_genetic_inspection_regions
            migrationBuilder.RenameTable(
                name: "genetic_inspection_regions",
                newName: "qpadm_genetic_inspection_regions");

            migrationBuilder.Sql(@"ALTER INDEX ""PK_genetic_inspection_regions"" RENAME TO ""PK_qpadm_genetic_inspection_regions"";");

            migrationBuilder.RenameIndex(
                name: "IX_genetic_inspection_regions_RegionId",
                table: "qpadm_genetic_inspection_regions",
                newName: "IX_qpadm_genetic_inspection_regions_RegionId");

            migrationBuilder.Sql(@"ALTER TABLE ""qpadm_genetic_inspection_regions"" RENAME CONSTRAINT ""FK_genetic_inspection_regions_qpadm_genetic_inspections_Geneti~"" TO ""FK_qpadm_genetic_inspection_regions_qpadm_genetic_inspections_~"";");
            migrationBuilder.Sql(@"ALTER TABLE ""qpadm_genetic_inspection_regions"" RENAME CONSTRAINT ""FK_genetic_inspection_regions_qpadm_regions_RegionId"" TO ""FK_qpadm_genetic_inspection_regions_qpadm_regions_RegionId"";");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // qpadm_genetic_inspection_regions -> genetic_inspection_regions
            migrationBuilder.Sql(@"ALTER TABLE ""qpadm_genetic_inspection_regions"" RENAME CONSTRAINT ""FK_qpadm_genetic_inspection_regions_qpadm_genetic_inspections_~"" TO ""FK_genetic_inspection_regions_qpadm_genetic_inspections_Geneti~"";");
            migrationBuilder.Sql(@"ALTER TABLE ""qpadm_genetic_inspection_regions"" RENAME CONSTRAINT ""FK_qpadm_genetic_inspection_regions_qpadm_regions_RegionId"" TO ""FK_genetic_inspection_regions_qpadm_regions_RegionId"";");

            migrationBuilder.RenameIndex(
                name: "IX_qpadm_genetic_inspection_regions_RegionId",
                table: "qpadm_genetic_inspection_regions",
                newName: "IX_genetic_inspection_regions_RegionId");

            migrationBuilder.Sql(@"ALTER INDEX ""PK_qpadm_genetic_inspection_regions"" RENAME TO ""PK_genetic_inspection_regions"";");

            migrationBuilder.RenameTable(
                name: "qpadm_genetic_inspection_regions",
                newName: "genetic_inspection_regions");

            // qpadm_populations -> populations
            migrationBuilder.Sql(@"ALTER TABLE ""qpadm_result_populations"" RENAME CONSTRAINT ""FK_qpadm_result_populations_qpadm_populations_PopulationId"" TO ""FK_qpadm_result_populations_populations_PopulationId"";");
            migrationBuilder.Sql(@"ALTER TABLE ""qpadm_populations"" RENAME CONSTRAINT ""FK_qpadm_populations_qpadm_eras_EraId"" TO ""FK_populations_eras_EraId"";");
            migrationBuilder.Sql(@"ALTER TABLE ""qpadm_populations"" RENAME CONSTRAINT ""FK_qpadm_populations_music_tracks_MusicTrackId"" TO ""FK_populations_music_tracks_MusicTrackId"";");

            migrationBuilder.RenameIndex(
                name: "IX_qpadm_populations_EraId",
                table: "qpadm_populations",
                newName: "IX_populations_EraId");

            migrationBuilder.RenameIndex(
                name: "IX_qpadm_populations_MusicTrackId",
                table: "qpadm_populations",
                newName: "IX_populations_MusicTrackId");

            migrationBuilder.Sql(@"ALTER INDEX ""PK_qpadm_populations"" RENAME TO ""PK_populations"";");

            migrationBuilder.RenameTable(
                name: "qpadm_populations",
                newName: "populations");

            // qpadm_eras -> eras
            migrationBuilder.Sql(@"ALTER TABLE ""qpadm_result_era_groups"" RENAME CONSTRAINT ""FK_qpadm_result_era_groups_qpadm_eras_EraId"" TO ""FK_qpadm_result_era_groups_eras_EraId"";");
            migrationBuilder.Sql(@"ALTER INDEX ""PK_qpadm_eras"" RENAME TO ""PK_eras"";");

            migrationBuilder.RenameTable(
                name: "qpadm_eras",
                newName: "eras");

            // qpadm_ethnicities -> ethnicities
            migrationBuilder.Sql(@"ALTER TABLE ""qpadm_regions"" RENAME CONSTRAINT ""FK_qpadm_regions_qpadm_ethnicities_EthnicityId"" TO ""FK_qpadm_regions_ethnicities_EthnicityId"";");
            migrationBuilder.Sql(@"ALTER INDEX ""PK_qpadm_ethnicities"" RENAME TO ""PK_ethnicities"";");

            migrationBuilder.RenameTable(
                name: "qpadm_ethnicities",
                newName: "ethnicities");
        }
    }
}
