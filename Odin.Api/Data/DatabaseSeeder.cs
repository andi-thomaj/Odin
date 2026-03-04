using Microsoft.EntityFrameworkCore;
using Odin.Api.Data.Entities;

namespace Odin.Api.Data;

public class DatabaseSeeder(ApplicationDbContext context)
{
    public async Task SeedAsync()
    {
        await SeedEthnicitiesAndRegionsAsync();
        await SeedErasPopulationsAndSubPopulationsAsync();
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

    private async Task SeedErasPopulationsAndSubPopulationsAsync()
    {
        if (await context.Eras.AnyAsync())
            return;

        var now = DateTime.UtcNow;
        const string seeder = "DatabaseSeeder";

        // era -> population -> sub-populations
        var seedData = new Dictionary<string, Dictionary<string, string[]>>
        {
            ["Bronze Age"] = new()
            {
                ["Mycenaean"] = ["Mycenae", "Pylos", "Tiryns"],
                ["Minoan"] = ["Knossos", "Phaistos", "Malia"],
                ["Yamnaya"] = ["Don-Volga", "Kalmykia", "Samara"],
                ["Corded Ware"] = ["Central European CW", "Baltic CW", "Scandinavian CW"],
                ["Bell Beaker"] = ["Iberian BB", "Central European BB", "British BB"],
            },
            ["Iron Age"] = new()
            {
                ["Classical Greek"] = ["Athenian", "Spartan", "Corinthian"],
                ["Etruscan"] = ["Northern Etruscan", "Southern Etruscan"],
                ["Celtic"] = ["Hallstatt", "La Tène", "Insular Celtic"],
                ["Scythian"] = ["Pontic Scythian", "Altai Scythian", "Saka"],
                ["Illyrian"] = ["Southern Illyrian", "Northern Illyrian", "Dardanian"],
            },
            ["Medieval"] = new()
            {
                ["Byzantine Greek"] = ["Anatolian Byzantine", "Balkan Byzantine", "Cretan Byzantine"],
                ["Slavic"] = ["South Slavic", "West Slavic", "East Slavic"],
                ["Viking"] = ["Norse", "Dane", "Swede"],
                ["Anglo-Saxon"] = ["East Anglian", "Kentish", "Mercian"],
                ["Lombard"] = ["Northern Lombard", "Southern Lombard"],
            },
            ["Modern"] = new()
            {
                ["Modern Greek"] = ["Peloponnesian", "Islander", "Macedonian Greek"],
                ["Modern Italian"] = ["Northern Italian", "Central Italian", "Southern Italian"],
                ["Modern Balkan"] = ["Albanian", "Serbian", "Bulgarian"],
                ["Modern Scandinavian"] = ["Norwegian", "Swedish", "Danish"],
            },
        };

        foreach (var (eraName, populations) in seedData)
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

            foreach (var (populationName, subPopulationNames) in populations)
            {
                var population = new Population
                {
                    Name = populationName,
                    Description = $"{populationName} population",
                    Era = era,
                    CreatedAt = now,
                    CreatedBy = seeder,
                    UpdatedAt = now,
                };
                context.Populations.Add(population);

                foreach (var subPopulationName in subPopulationNames)
                {
                    context.SubPopulations.Add(new SubPopulation
                    {
                        Name = subPopulationName,
                        Description = $"{subPopulationName} sub-population",
                        Population = population,
                        CreatedAt = now,
                        CreatedBy = seeder,
                        UpdatedAt = now,
                    });
                }
            }
        }

        await context.SaveChangesAsync();
    }
}
