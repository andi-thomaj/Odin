using Microsoft.EntityFrameworkCore;
using Odin.Api.Data.Entities;

namespace Odin.Api.Data.Seeders;

/// <summary>
/// Seeds the qpAdm ethnicity catalog and the regions hanging off each ethnicity.
/// No-ops once the table contains any rows.
/// </summary>
internal sealed class EthnicityAndRegionSeeder(ApplicationDbContext context)
{
    public async Task SeedAsync()
    {
        if (await context.QpadmEthnicities.AnyAsync())
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
            var ethnicity = new QpadmEthnicity { Name = ethnicityName };
            context.QpadmEthnicities.Add(ethnicity);

            foreach (var regionName in regionNames)
            {
                context.QpadmRegions.Add(new QpadmRegion { Name = regionName, Ethnicity = ethnicity });
            }
        }

        await context.SaveChangesAsync();
    }
}
