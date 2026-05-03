using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;
using Npgsql;
using Odin.Api.Data;

namespace Odin.Api.Tests.SeedDataExport;

public class G25DistancePopulationSamplesExportTests
{
    [Fact]
    public async Task Export_DistancePopulationSamples_RewritesSeedJson()
    {
        var connectionString = ResolveConnectionString();
        Assert.False(
            string.IsNullOrWhiteSpace(connectionString),
            "ConnectionStrings:DefaultConnection must be set in Odin.Api user-secrets " +
            "or via the ConnectionStrings__DefaultConnection environment variable.");

        const string sql = """
            SELECT
              g."Label" AS "SampleLabel",
              g."Coordinates",
              g."Ids",
              g."G25DistanceEraId",
              json_agg(json_build_object('Label', r."Label", 'Link', r."Link"))
                FILTER (WHERE r."Id" IS NOT NULL) AS "ResearchLinks"
            FROM public.g25_distance_population_samples g
            LEFT JOIN public.research_links r
              ON g."Id" = r."G25DistancePopulationSampleId"
            GROUP BY
              g."Id",
              g."Label",
              g."Coordinates",
              g."Ids",
              g."G25DistanceEraId"
            ORDER BY g."Id";
            """;

        var deserializeOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
        };

        var samples = new List<SampleRow>();

        await using (var connection = new NpgsqlConnection(connectionString))
        {
            await connection.OpenAsync();

            await using var command = new NpgsqlCommand(sql, connection);
            await using var reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                var sampleLabel = reader.GetString(0);
                var coordinates = reader.GetString(1);
                var ids = reader.GetString(2);
                var eraId = reader.IsDBNull(3) ? (int?)null : reader.GetInt32(3);

                List<ResearchLinkRow>? researchLinks = null;
                if (!reader.IsDBNull(4))
                {
                    var json = reader.GetString(4);
                    researchLinks = JsonSerializer
                        .Deserialize<List<ResearchLinkRow>>(json, deserializeOptions);
                }

                samples.Add(new SampleRow(sampleLabel, coordinates, ids, eraId, researchLinks));
            }
        }

        Assert.NotEmpty(samples);

        var writeOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.Never,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        };

        var output = JsonSerializer.Serialize(samples, writeOptions);

        var seedPath = ResolveSeedFilePath();
        await File.WriteAllTextAsync(seedPath, output);
    }

    private static string ResolveConnectionString()
    {
        var fromEnv = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection");
        if (!string.IsNullOrWhiteSpace(fromEnv))
            return fromEnv.Trim();

        var config = new ConfigurationBuilder()
            .AddUserSecrets(typeof(ApplicationDbContext).Assembly)
            .Build();

        return config.GetConnectionString("DefaultConnection") ?? string.Empty;
    }

    private static string ResolveSeedFilePath()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Odin.slnx")))
            dir = dir.Parent;

        if (dir is null)
            throw new InvalidOperationException(
                "Could not locate Odin.slnx — unable to resolve the seed-data file path.");

        return Path.Combine(
            dir.FullName,
            "Odin.Api",
            "Data",
            "SeedData",
            "g25_distance_population_samples.json");
    }

    private sealed record SampleRow(
        string SampleLabel,
        string Coordinates,
        string Ids,
        int? G25DistanceEraId,
        List<ResearchLinkRow>? ResearchLinks);

    private sealed record ResearchLinkRow(string Label, string Link);
}
