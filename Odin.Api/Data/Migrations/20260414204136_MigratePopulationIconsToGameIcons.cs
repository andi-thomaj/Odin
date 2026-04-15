using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Odin.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class MigratePopulationIconsToGameIcons : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Replace legacy `*.svg` filenames with @iconify-json/game-icons ids so the
            // frontend can render them as tinted SVG data URLs from a single shared library
            // (see odin-react/src/utils/iconify-game-icons.ts). Only rows that still hold the
            // legacy filename are updated, so the migration is idempotent and safe to re-run.
            var populationIcons = new (string Name, string IconId)[]
            {
                ("Anatolian Neolithic Farmer", "game-icons:wheat"),
                ("Western Steppe Herder", "game-icons:horse-head"),
                ("Western Hunter Gatherer", "game-icons:stag-head"),
                ("Caucasian Hunter Gatherer", "game-icons:bow-arrow"),
                ("Iranian Neolithic Farmer", "game-icons:goat"),
                ("Natufian", "game-icons:sickle"),
                ("North African Farmer", "game-icons:palm-tree"),
                ("Northeast Asian", "game-icons:pine-tree"),
                ("Native American", "game-icons:tipi"),
                ("Ancestral South Indian", "game-icons:lotus"),
                ("Sub Saharan Africans", "game-icons:tribal-shield"),
                ("Illyrian", "game-icons:barbed-spear"),
                ("Ancient Greek", "game-icons:greek-temple"),
                ("Thracian", "game-icons:horseshoe"),
                ("Hittite & Phrygian", "game-icons:chariot"),
                ("Phoenician", "game-icons:trireme"),
                ("Celtic", "game-icons:triquetra"),
                ("Iberian", "game-icons:bull-horns"),
                ("Punic Carthage", "game-icons:galleon"),
                ("Hellenistic Pontus", "game-icons:spartan-helmet"),
                ("Roman West Anatolia", "game-icons:ancient-columns"),
                ("Latin and Etruscan", "game-icons:roman-shield"),
                ("Roman Moesia Superior", "game-icons:centurion-helmet"),
                ("Roman East Mediterranean", "game-icons:roman-toga"),
                ("Germanic", "game-icons:axe-sword"),
                ("Medieval Slavic", "game-icons:axe-in-stump"),
                ("Roman North Africa", "game-icons:scarab-beetle"),
                ("Baltic", "game-icons:gem-pendant"),
                ("Finno-Ugric", "game-icons:deer-head"),
                ("Saami", "game-icons:tribal-mask"),
            };

            foreach (var (name, iconId) in populationIcons)
            {
                var escapedName = name.Replace("'", "''");
                migrationBuilder.Sql(
                    $"UPDATE populations SET \"IconFileName\" = '{iconId}' " +
                    $"WHERE \"Name\" = '{escapedName}' " +
                    $"AND \"IconFileName\" NOT LIKE 'game-icons:%';");
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Revert iconify ids back to the legacy `*.svg` filenames. Mirrors the Up mapping.
            var populationIcons = new (string Name, string LegacyFile)[]
            {
                ("Anatolian Neolithic Farmer", "anatolian-neolithic-farmer.svg"),
                ("Western Steppe Herder", "western-steppe-herder.svg"),
                ("Western Hunter Gatherer", "western-hunter-gatherer.svg"),
                ("Caucasian Hunter Gatherer", "caucasian-hunter-gatherer.svg"),
                ("Iranian Neolithic Farmer", "iranian-neolithic-farmer.svg"),
                ("Natufian", "natufian.svg"),
                ("North African Farmer", "north-african-farmer.svg"),
                ("Northeast Asian", "northeast-asian-neolithic.svg"),
                ("Native American", "native-american.svg"),
                ("Ancestral South Indian", "ancient-ancestral-south-indian.svg"),
                ("Sub Saharan Africans", "sub-saharan-african.svg"),
                ("Illyrian", "illyrian.svg"),
                ("Ancient Greek", "ancient-greek.svg"),
                ("Thracian", "thracian.svg"),
                ("Hittite & Phrygian", "hittite-phrygian.svg"),
                ("Phoenician", "phoenician.svg"),
                ("Celtic", "insular-celt.svg"),
                ("Iberian", "iberian.svg"),
                ("Punic Carthage", "punic-carthage.svg"),
                ("Hellenistic Pontus", "hellenistic-pontus.svg"),
                ("Roman West Anatolia", "hellenistic-pontus.svg"),
                ("Latin and Etruscan", "italic-and-etruscan.svg"),
                ("Roman Moesia Superior", "roman-moesia.svg"),
                ("Roman East Mediterranean", "roman-east-mediterranean.svg"),
                ("Germanic", "germanic.svg"),
                ("Medieval Slavic", "medieval-slav.svg"),
                ("Roman North Africa", "berber.svg"),
                ("Baltic", "western-hunter-gatherer.svg"),
                ("Finno-Ugric", "western-steppe-herder.svg"),
                ("Saami", "northeast-asian-neolithic.svg"),
            };

            foreach (var (name, legacyFile) in populationIcons)
            {
                var escapedName = name.Replace("'", "''");
                migrationBuilder.Sql(
                    $"UPDATE populations SET \"IconFileName\" = '{legacyFile}' " +
                    $"WHERE \"Name\" = '{escapedName}';");
            }
        }
    }
}
