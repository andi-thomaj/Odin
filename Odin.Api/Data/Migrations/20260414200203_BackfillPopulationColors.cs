using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Odin.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class BackfillPopulationColors : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Backfill Color for populations seeded before 20260414191846_AddColorToPopulations,
            // which added the column with an empty-string default. Only rows whose Color is still
            // the empty default are updated, so the migration is safe to re-run or apply to a DB
            // that was seeded by DatabaseSeeder (which already sets Color on insert).
            var populationColors = new (string Name, string Color)[]
            {
                ("Anatolian Neolithic Farmer", "#E69F00"),
                ("Western Steppe Herder", "#C0392B"),
                ("Western Hunter Gatherer", "#1F4E79"),
                ("Caucasian Hunter Gatherer", "#7F8C8D"),
                ("Iranian Neolithic Farmer", "#27AE60"),
                ("Natufian", "#6A0DAD"),
                ("North African Farmer", "#D4AC0D"),
                ("Northeast Asian", "#2980B9"),
                ("Native American", "#A93226"),
                ("Ancestral South Indian", "#8E44AD"),
                ("Sub Saharan Africans", "#4A2C2A"),
                ("Illyrian", "#A93226"),
                ("Ancient Greek", "#355C9A"),
                ("Thracian", "#1E8449"),
                ("Hittite & Phrygian", "#AF7AC5"),
                ("Phoenician", "#6A0DAD"),
                ("Celtic", "#196F3D"),
                ("Iberian", "#CA6F1E"),
                ("Punic Carthage", "#1C2833"),
                ("Hellenistic Pontus", "#5DADE2"),
                ("Roman West Anatolia", "#1F7A6E"),
                ("Latin and Etruscan", "#922B21"),
                ("Roman Moesia Superior", "#7DCEA0"),
                ("Roman East Mediterranean", "#1B4F72"),
                ("Germanic", "#566573"),
                ("Medieval Slavic", "#DCCB6E"),
                ("Roman North Africa", "#D4AC0D"),
                ("Baltic", "#F1C40F"),
                ("Finno-Ugric", "#2C3E50"),
                ("Saami", "#E74C3C"),
            };

            foreach (var (name, color) in populationColors)
            {
                var escapedName = name.Replace("'", "''");
                migrationBuilder.Sql(
                    $"UPDATE populations SET \"Color\" = '{color}' WHERE \"Name\" = '{escapedName}' AND \"Color\" = '';");
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Best-effort reset: clear Color back to the empty default for any population
            // this migration may have touched. Schema revert (removing the column) is handled
            // by the previous migration's Down.
            migrationBuilder.Sql("UPDATE populations SET \"Color\" = '';");
        }
    }
}
