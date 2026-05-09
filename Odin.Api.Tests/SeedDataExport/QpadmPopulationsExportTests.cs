using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;
using Npgsql;
using Odin.Api.Data;

namespace Odin.Api.Tests.SeedDataExport;

public class QpadmPopulationsExportTests
{
    [Fact]
    public async Task Export_QpadmPopulations_RewritesSeedJson()
    {
        var connectionString = ResolveConnectionString();
        Assert.False(
            string.IsNullOrWhiteSpace(connectionString),
            "ConnectionStrings:DefaultConnection must be set in Odin.Api user-secrets " +
            "or via the ConnectionStrings__DefaultConnection environment variable.");

        const string sql = """
            SELECT
              q."Name",
              m."FileName" AS "MusicTrackFileName",
              q."Description",
              q."GeoJson",
              q."IconFileName",
              q."Color",
              q."EraId"
            FROM public.qpadm_populations q
            LEFT JOIN public.music_tracks m
              ON q."MusicTrackId" = m."Id"
            ORDER BY q."Id";
            """;

        var populations = new List<PopulationRow>();

        await using (var connection = new NpgsqlConnection(connectionString))
        {
            await connection.OpenAsync();

            await using var command = new NpgsqlCommand(sql, connection);
            await using var reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                var name = reader.GetString(0);
                var musicTrackFileName = reader.IsDBNull(1) ? string.Empty : reader.GetString(1);
                var description = reader.GetString(2);
                var geoJson = reader.GetString(3);
                var iconFileName = reader.GetString(4);
                var color = reader.GetString(5);
                var eraId = reader.GetInt32(6);

                populations.Add(new PopulationRow(
                    name,
                    musicTrackFileName,
                    description,
                    geoJson,
                    iconFileName,
                    color,
                    eraId));
            }
        }

        Assert.NotEmpty(populations);

        var writeOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.Never,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        };

        var output = JsonSerializer.Serialize(populations, writeOptions);

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
            "qpadm-populations.json");
    }

    private sealed record PopulationRow(
        string Name,
        string MusicTrackFileName,
        string Description,
        string GeoJson,
        string IconFileName,
        string Color,
        int EraId);
}
