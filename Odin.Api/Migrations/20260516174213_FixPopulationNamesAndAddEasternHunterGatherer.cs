using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Odin.Api.Migrations
{
    /// <summary>
    /// Reconciles the qpadm_populations table with the current state of
    /// <c>Data/SeedData/qpadm-populations.json</c>. Production was originally seeded from an
    /// older revision of that JSON which had typos in six names and was missing Eastern Hunter
    /// Gatherer; <see cref="Odin.Api.Data.Seeders.QpadmEraAndPopulationSeeder"/> is one-shot
    /// (it bails out via <c>AnyAsync</c> if eras already exist), so subsequent JSON edits never
    /// reached previously-seeded DBs. The disk MP4 filenames were renamed to match the corrected
    /// JSON, which is why <c>SyncVideoAvatarsFromDiskAsync</c> currently misses these 7 rows —
    /// it looks up <c>{population.Name}.mp4</c> using the stale DB name.
    ///
    /// All operations are idempotent: UPDATEs match by the OLD name (no-op after the first run
    /// or on freshly seeded DBs), and the INSERT is guarded by <c>NOT EXISTS</c>.
    /// </summary>
    public partial class FixPopulationNamesAndAddEasternHunterGatherer : Migration
    {
        private const string Author = "FixPopulationNamesAndAddEasternHunterGatherer";

        private static readonly (string Old, string New)[] Renames =
        {
            ("Natufian (12,000 - 8000 BC)", "Natufian (12000 - 8000 BC)"),
            ("Ancestral South Indian (10,000 - 2000 BC)", "Ancestral South Indian (10000 - 2000 BC)"),
            ("Sub Saharan Africans", "Sub Saharan African"),
            ("West Anatolia (0 - 600 AD)", "West Anatolian (0 - 600 AD)"),
            ("East Mediterranean (0 - 600 AD)", "Eastern Mediterranean (0 - 600 AD)"),
            ("North Africa (0 - 500 AD)", "North African (0 - 500 AD)"),
        };

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            foreach (var (oldName, newName) in Renames)
            {
                migrationBuilder.Sql(
                    "UPDATE qpadm_populations " +
                    "SET \"Name\" = '" + newName + "', " +
                        "\"UpdatedAt\" = NOW(), " +
                        "\"UpdatedBy\" = '" + Author + "' " +
                    "WHERE \"Name\" = '" + oldName + "';");
            }

            // Eastern Hunter Gatherer was added to the seed JSON after the initial production
            // seed, so it doesn't exist in any pre-existing DB. EraId is the static seed value 1
            // ("Hunter Gatherer and Neolithic Farmer"); MusicTrackId is looked up by FileName so
            // we don't depend on the auto-generated id assigned by the seeder.
            //
            // The trailing EXISTS clause guards fresh-DB startups where migrations run before
            // the seeder has populated music_tracks — without it the subquery would return NULL
            // and the NOT NULL MusicTrackId would crash the migration. On a fresh DB this insert
            // is a no-op and the seeder adds Eastern Hunter Gatherer from JSON as normal.
            migrationBuilder.Sql(@"
INSERT INTO qpadm_populations (
    ""Name"", ""Description"", ""GeoJson"", ""IconFileName"", ""Color"",
    ""EraId"", ""MusicTrackId"", ""VideoAvatarVersion"",
    ""CreatedAt"", ""CreatedBy"", ""UpdatedAt"", ""UpdatedBy""
)
SELECT
    'Eastern Hunter Gatherer (12000 - 5000 BC)',
    $desc$Represents the Mesolithic forager populations of Eastern Europe (~12,000–5,000 BCE), inhabiting the forest-steppe and taiga belt from Karelia and the eastern Baltic across the Volga basin to the western Urals. EHGs were first directly characterised through the landmark Haak et al. (2015) and Mathieson et al. (2015) studies of individuals from Yuzhny Oleny Ostrov on Lake Onega in Karelia (~7,500 BP), Popovo in Vologda, and the Sidelkino (~10,700 BP) and Lebyazhinka burials in the Samara region. Their genetic profile combines a deeply divergent western forager component related to Western Hunter-Gatherers with substantial Ancient North Eurasian (ANE) ancestry traceable to the Mal'ta-Buret' culture of south-central Siberia (~24,000 BP). EHGs showed mixed pigmentation, with derived alleles for lighter skin colour appearing at higher frequencies than in contemporary Western Hunter-Gatherers and eye colour varying from blue to brown alongside predominantly dark hair. EHG ancestry was the foundational source of the Western Steppe Herder gene pool through admixture with Caucasus Hunter-Gatherers around the 5th millennium BCE, and persists at substantial proportions in modern populations of Northern and Eastern Europe, the Baltic states, and Volga-Uralic peoples, as well as indirectly across Eurasia through Bronze Age steppe-derived migrations. Y-chromosome haplogroups R1a-M459 and J were prevalent among EHG individuals, with descendant lineages contributing ancestrally to the dominant modern European R1a and R1b clades.$desc$,
    '{""type"":""Polygon"",""coordinates"":[[[30.0,62.0],[38.0,65.0],[56.0,60.0],[58.0,55.0],[52.0,51.0],[42.0,51.0],[32.0,51.0],[28.0,55.0],[30.0,62.0]]]}',
    'game-icons:wolf-head',
    '#34495E',
    1,
    (SELECT ""Id"" FROM music_tracks WHERE ""FileName"" = 'european-foragers.wav' LIMIT 1),
    NULL,
    NOW(), 'FixPopulationNamesAndAddEasternHunterGatherer',
    NOW(), 'FixPopulationNamesAndAddEasternHunterGatherer'
WHERE NOT EXISTS (
    SELECT 1 FROM qpadm_populations WHERE ""Name"" = 'Eastern Hunter Gatherer (12000 - 5000 BC)'
)
  AND EXISTS (
    SELECT 1 FROM music_tracks WHERE ""FileName"" = 'european-foragers.wav'
);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "DELETE FROM qpadm_populations WHERE \"Name\" = 'Eastern Hunter Gatherer (12000 - 5000 BC)';");

            foreach (var (oldName, newName) in Renames)
            {
                migrationBuilder.Sql(
                    "UPDATE qpadm_populations " +
                    "SET \"Name\" = '" + oldName + "', " +
                        "\"UpdatedAt\" = NOW(), " +
                        "\"UpdatedBy\" = '" + Author + "' " +
                    "WHERE \"Name\" = '" + newName + "';");
            }
        }
    }
}
