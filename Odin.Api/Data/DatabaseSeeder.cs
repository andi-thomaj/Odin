using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Odin.Api.Data.Entities;
using Odin.Api.Data.Enums;

namespace Odin.Api.Data;

public class DatabaseSeeder(ApplicationDbContext context)
{
    public async Task SeedAsync()
    {
        await SeedReferenceCatalogAsync();
    }

    /// <summary>Ethnicities, eras, geo backfill, commerce catalog — safe to re-run when tables are empty (e.g. integration tests after Respawn).</summary>
    public async Task SeedReferenceCatalogAsync()
    {
        await SeedEthnicitiesAndRegionsAsync();
        await SeedErasAndPopulationsAsync();
        await SeedMusicTracksAsync();
        await SeedCatalogCommerceAsync();
    }

    public async Task SeedCatalogCommerceAsync()
    {
        if (await context.CatalogProducts.AnyAsync())
            return;

        var product = new CatalogProduct
        {
            ServiceType = OrderService.qpAdm,
            DisplayName = "qpAdm ancestry analysis",
            Description = "Deep ancestry modeling with reference populations.",
            BasePrice = 49.99m,
            IsActive = true
        };
        context.CatalogProducts.Add(product);
        await context.SaveChangesAsync();

        var addons = new[]
        {
            new ProductAddon
            {
                Code = "EXPEDITED",
                DisplayName = "Compute faster your results",
                Price = 20m,
                IsActive = true
            },
            new ProductAddon
            {
                Code = "Y_HAPLOGROUP",
                DisplayName = "Find your Y haplogroup",
                Price = 20m,
                IsActive = true
            },
            new ProductAddon
            {
                Code = "MERGE_RAW",
                DisplayName = "Merge your raw data",
                Price = 40m,
                IsActive = true
            }
        };
        context.ProductAddons.AddRange(addons);
        await context.SaveChangesAsync();

        foreach (var addon in addons)
        {
            context.CatalogProductAddons.Add(new CatalogProductAddon
            {
                CatalogProductId = product.Id,
                ProductAddonId = addon.Id
            });
        }

        context.PromoCodes.Add(new PromoCode
        {
            Code = "WELCOME10",
            DiscountType = PromoDiscountType.Percent,
            Value = 10m,
            IsActive = true,
            ApplicableService = OrderService.qpAdm,
            RedemptionCount = 0
        });

        await context.SaveChangesAsync();
    }

    private async Task SeedEthnicitiesAndRegionsAsync()
    {
        if (await context.Ethnicities.AnyAsync())
            return;

        var seedData = new Dictionary<string, string[]>
        {
            ["Greek"] =
            [
                "Peloponnese", "Attica", "Islander Greek", "Macedonia", "Thrace",
                "Epirus", "Thessaly", "Central Greece", "Magna Graecia",
                "Anatolian Greek", "Pontic Greek", "North Epirote Greek",
            ],
            ["Albanian"] =
            [
                "South Albanian", "North Albanian", "Kosovo Albanian", "Sanjak Albanian",
                "Serbia Albanian", "Macedonia Albanian", "Arvanite",
                "Greek Epirus Albanian", "Arbereshe",
            ],
            ["Serbian"] =
            [
                "Serbia Serbian", "RS Serbian (Republika Srpska Bosnia)",
                "Montenegro", "Kosovo",
            ],
            ["Turkish"] = ["Anatolian Turk", "Balkan Turk"],
            ["Bulgarian"] =
            [
                "East Bulgaria", "West Bulgaria", "North Macedonia Bulgarian",
                "Greek Thrace", "Turkey Bulgarian",
            ],
            ["Italian"] =
            [
                "North Italian", "Central Italian", "South Italian",
                "Sicilian", "Sardinian", "Corsica",
            ],
            ["North Macedonian"] = ["East Macedonia", "West Macedonia", "South Macedonia"],
            ["Pomak"] = [],
        };

        foreach (var (ethnicityName, regionNames) in seedData)
        {
            var ethnicity = new Ethnicity { Name = ethnicityName };
            context.Ethnicities.Add(ethnicity);

            foreach (var regionName in regionNames)
            {
                context.Regions.Add(new Region { Name = regionName, Ethnicity = ethnicity });
            }
        }

        await context.SaveChangesAsync();
    }

    private async Task SeedErasAndPopulationsAsync()
    {
        if (await context.Eras.AnyAsync())
            return;

        var now = DateTime.UtcNow;
        const string seeder = "DatabaseSeeder";

        var geoJsonMap = LoadPopulationGeoJson();

        var seedData = new Dictionary<string, string[]>
        {
            ["Hunter Gatherer and Neolithic Farmer"] =
            [
                "Anatolian Neolithic Farmer",
                "Western Steppe Herder",
                "Western Hunter Gatherer",
                "Caucasian Hunter Gatherer",
                "Iranian Neolithic Farmer",
                "Natufian",
                "North African Farmer",
                "Northeast Asian",
                "Native American",
                "Ancestral South Indian",
                "Sub Saharan Africans",
                "Baltic",
                "Finno-Ugric",
                "Saami",
            ],
            ["Iron Age and Migration Period"] =
            [
                "Illyrian",
                "Ancient Greek",
                "Thracian",
                "Hittite & Phrygian",
                "Phoenician",
                "Celtic",
                "Iberian",
                "Punic Carthage",
                "Hellenistic Pontus",
                "Latin and Etruscan",
                "Roman Moesia Superior",
                "Medieval Albanian",
                "Roman East Mediterranean",
                "Germanic",
                "Medieval Slavic",
                "Roman North Africa",
                "Roman West Anatolia",
            ],
        };

        foreach (var (eraName, populationNames) in seedData)
        {
            var era = new Era
            {
                Name = eraName,
                Description = $"{eraName} era",
                CreatedAt = now,
                CreatedBy = seeder,
                UpdatedAt = now,
            };
            context.Eras.Add(era);

            foreach (var populationName in populationNames)
            {
                context.Populations.Add(new Population
                {
                    Name = populationName,
                    Description = $"{populationName} population",
                    Era = era,
                    GeoJson = geoJsonMap.GetValueOrDefault(populationName),
                    CreatedAt = now,
                    CreatedBy = seeder,
                    UpdatedAt = now,
                });
            }
        }

        await context.SaveChangesAsync();
    }

    private async Task SeedMusicTracksAsync()
    {
        if (await context.MusicTracks.AnyAsync())
            return;

        var now = DateTime.UtcNow;
        const string seeder = "DatabaseSeeder";

        // Music culture groupings: (displayOrder, name, fileName, populationNames[])
        var musicTrackData = new (int Order, string Name, string FileName, string[] Populations)[]
        {
            // Era 1 — Hunter Gatherer and Neolithic Farmer
            (1, "European Foragers", "european-foragers.wav", ["Western Hunter Gatherer", "Baltic", "Finno-Ugric", "Saami"]),
            (2, "Eurasian Steppe", "eurasian-steppe.wav", ["Western Steppe Herder"]),
            (3, "Near Eastern / Anatolian Farmers", "near-eastern-farmers.wav",
                ["Anatolian Neolithic Farmer", "Iranian Neolithic Farmer", "Caucasian Hunter Gatherer", "Ancestral South Indian"]),
            (4, "Levantine & North African", "levantine-north-african.wav", ["Natufian", "North African Farmer"]),
            (5, "East Asian & Native American", "east-asian-native-american.wav", ["Northeast Asian", "Native American"]),
            (6, "Sub-Saharan African", "sub-saharan-african.wav", ["Sub Saharan Africans"]),

            // Era 2 — Iron Age and Migration Period
            (7, "Hellenic", "hellenic.wav",
                ["Ancient Greek", "Roman West Anatolia", "Hellenistic Pontus", "Roman East Mediterranean"]),
            (8, "Roman / Italic", "roman-italic.wav", ["Latin and Etruscan", "Roman Moesia Superior", "Roman North Africa"]),
            (9, "Balkan / Paleo-Balkan", "balkan-paleo-balkan.wav", ["Illyrian", "Thracian", "Medieval Albanian"]),
            (10, "Anatolian / Caucasian", "anatolian-caucasian.wav", ["Hittite & Phrygian"]),
            (11, "Semitic / Phoenician", "semitic-phoenician.wav", ["Phoenician", "Punic Carthage"]),
            (12, "Celtic", "celtic.wav", ["Celtic"]),
            (13, "Western Mediterranean", "western-mediterranean-pre-ie.wav", ["Iberian"]),
            (14, "Germanic", "germanic-sarmatian.wav", ["Germanic"]),
            (15, "Medieval Slavic", "medieval-slavic.wav", ["Medieval Slavic"]),
        };

        // Load all populations for linking
        var populations = await context.Populations.ToListAsync();
        var populationsByName = populations.ToDictionary(p => p.Name);

        foreach (var (order, name, fileName, populationNames) in musicTrackData)
        {
            var track = new MusicTrack
            {
                Name = name,
                FileName = fileName,
                DisplayOrder = order,
                CreatedAt = now,
                CreatedBy = seeder,
                UpdatedAt = now,
            };
            context.MusicTracks.Add(track);

            foreach (var popName in populationNames)
            {
                if (populationsByName.TryGetValue(popName, out var population))
                {
                    population.MusicTrackId = track.Id;
                    population.MusicTrack = track;
                }
            }
        }

        await context.SaveChangesAsync();
    }

    private static Dictionary<string, string> LoadPopulationGeoJson()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Data", "SeedData", "population-geojson.json");
        if (!File.Exists(path))
            return new Dictionary<string, string>();

        var json = File.ReadAllText(path);
        var raw = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json);
        if (raw is null)
            return new Dictionary<string, string>();

        var result = new Dictionary<string, string>();
        foreach (var (name, geometry) in raw)
        {
            result[name] = geometry.GetRawText();
        }
        return result;
    }

}
