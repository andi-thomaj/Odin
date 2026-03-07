using Microsoft.EntityFrameworkCore;
using Odin.Api.Data.Entities;
using Odin.Api.Data.Enums;

namespace Odin.Api.Data;

public class DatabaseSeeder(ApplicationDbContext context)
{
    public async Task SeedAsync()
    {
        await SeedEthnicitiesAndRegionsAsync();
        await SeedErasPopulationsAndSubPopulationsAsync();
        await SeedUsersOrdersAndGeneticFilesAsync();
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
            ["Hunter Gatherer & Neolithic Farmer"] = new()
            {
                ["Anatolian Neolithic Farmer"] = [],
                ["Western Steppe Herder"] = [],
                ["Western Hunter Gatherer"] = [],
                ["Caucasian Hunter Gatherer"] = [],
                ["Iranian Neolithic Farmer"] = [],
                ["Natufian"] = [],
                ["North African Farmer"] = [],
                ["Northeast Asian Neolithic"] = [],
                ["Native American"] = [],
                ["Ancient Ancestral South Indian"] = [],
                ["Sub Saharan African"] = [],
                ["Uralic"] = [],
            },
            ["Iron Age & Migration Period"] = new()
            {
                ["Illyrian"] = [],
                ["Ancient Greek"] = [],
                ["Thracian"] = [],
                ["Hittite & Phrygian"] = [],
                ["Phoenician"] = [],
                ["Insular Celt"] = [],
                ["Continental Celt"] = [],
                ["Iberian"] = [],
                ["Punic Carthage"] = [],
                ["Berber"] = [],
                ["Hellenistic Pontus"] = [],
                ["Siberian"] = [],
                ["Sicani"] = [],
                ["Italic and Etruscan"] = [],
                ["Sarmatian"] = [],
                ["Colchian"] = [],
                ["Roman Moesia"] = [],
                ["Proto-Albanian"] = [],
                ["Roman Greece"] = [],
                ["Roman Gaul"] = [],
                ["Roman East Mediterranean"] = [],
                ["Germanic"] = [],
                ["Medieval Slav"] = [],
                ["Turkic"] = [],
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

    private async Task SeedUsersOrdersAndGeneticFilesAsync()
    {
        if (await context.Orders.AnyAsync())
            return;

        var now = DateTime.UtcNow;
        const string seeder = "DatabaseSeeder";

        // Seed users
        var users = new[]
        {
            new User
            {
                IdentityId = "auth0|seed-user-001",
                Username = "jdoe",
                Email = "jdoe@example.com",
                FirstName = "John",
                LastName = "Doe",
                Role = AppRole.Scientist,
                CreatedAt = now,
                CreatedBy = seeder,
                UpdatedAt = now,
            },
            new User
            {
                IdentityId = "auth0|seed-user-002",
                Username = "asmith",
                Email = "asmith@example.com",
                FirstName = "Anna",
                LastName = "Smith",
                Role = AppRole.User,
                CreatedAt = now,
                CreatedBy = seeder,
                UpdatedAt = now,
            },
        };

        if (!await context.Users.AnyAsync())
        {
            context.Users.AddRange(users);
            await context.SaveChangesAsync();
        }
        else
        {
            users = await context.Users.OrderBy(u => u.Id).Take(2).ToArrayAsync();
        }

        // Seed raw genetic files
        var sampleCsvContent =
            "rsid,chromosome,position,genotype\nrs1234567,1,100000,AG\nrs7654321,2,200000,CT\nrs1111111,3,300000,GG"u8
                .ToArray();

        var geneticFiles = new[]
        {
            new RawGeneticFile
            {
                FileName = "sample_dna_johndoe.csv",
                RawData = sampleCsvContent,
                CreatedAt = now,
                CreatedBy = seeder,
                UpdatedAt = now,
            },
            new RawGeneticFile
            {
                FileName = "sample_dna_annasmith.csv",
                RawData = sampleCsvContent,
                CreatedAt = now,
                CreatedBy = seeder,
                UpdatedAt = now,
            },
            new RawGeneticFile
            {
                FileName = "sample_dna_johndoe_v2.csv",
                RawData = sampleCsvContent,
                CreatedAt = now,
                CreatedBy = seeder,
                UpdatedAt = now,
            },
        };

        context.RawGeneticFiles.AddRange(geneticFiles);
        await context.SaveChangesAsync();

        // Seed orders
        var orders = new[]
        {
            new Order
            {
                Price = 49.99m,
                Service = Enums.OrderService.QPADM,
                Status = OrderStatus.Completed,
                CreatedAt = now.AddDays(-30),
                CreatedBy = seeder,
                UpdatedAt = now.AddDays(-25),
                UpdatedBy = seeder,
            },
            new Order
            {
                Price = 29.99m,
                Service = Enums.OrderService.Vahaduo,
                Status = OrderStatus.InProcess,
                CreatedAt = now.AddDays(-10),
                CreatedBy = seeder,
                UpdatedAt = now.AddDays(-5),
                UpdatedBy = seeder,
            },
            new Order
            {
                Price = 49.99m,
                Service = Enums.OrderService.QPADM,
                Status = OrderStatus.Pending,
                CreatedAt = now.AddDays(-1),
                CreatedBy = seeder,
                UpdatedAt = now.AddDays(-1),
            },
        };

        context.Orders.AddRange(orders);
        await context.SaveChangesAsync();

        // Fetch regions for inspection associations
        var regions = await context.Regions.Take(3).ToListAsync();

        // Seed genetic inspections linking users, files, and orders
        var inspections = new[]
        {
            new GeneticInspection
            {
                UserId = users[0].Id,
                FirstName = "John",
                MiddleName = string.Empty,
                LastName = "Doe",
                RawGeneticFileId = geneticFiles[0].Id,
                OrderId = orders[0].Id,
                CreatedAt = now.AddDays(-30),
                CreatedBy = seeder,
                UpdatedAt = now.AddDays(-25),
            },
            new GeneticInspection
            {
                UserId = users[1].Id,
                FirstName = "Anna",
                MiddleName = string.Empty,
                LastName = "Smith",
                RawGeneticFileId = geneticFiles[1].Id,
                OrderId = orders[1].Id,
                CreatedAt = now.AddDays(-10),
                CreatedBy = seeder,
                UpdatedAt = now.AddDays(-5),
            },
            new GeneticInspection
            {
                UserId = users[0].Id,
                FirstName = "John",
                MiddleName = "M",
                LastName = "Doe",
                RawGeneticFileId = geneticFiles[2].Id,
                OrderId = orders[2].Id,
                CreatedAt = now.AddDays(-1),
                CreatedBy = seeder,
                UpdatedAt = now.AddDays(-1),
            },
        };

        context.GeneticInspections.AddRange(inspections);
        await context.SaveChangesAsync();

        // Add region associations for each inspection
        foreach (var inspection in inspections)
        {
            foreach (var region in regions)
            {
                context.GeneticInspectionRegions.Add(new GeneticInspectionRegion
                {
                    GeneticInspectionId = inspection.Id,
                    GeneticInspection = inspection,
                    RegionId = region.Id,
                    Region = region,
                });
            }
        }

        await context.SaveChangesAsync();
    }
}
