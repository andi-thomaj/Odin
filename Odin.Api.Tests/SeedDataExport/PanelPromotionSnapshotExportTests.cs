using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Npgsql;
using Odin.Api.Data;
using Odin.Api.Endpoints.Admin.Models;

namespace Odin.Api.Tests.SeedDataExport;

/// <summary>
/// Manual seed-export utilities that rewrite the two panel-promotion snapshot files in the repo from
/// a live <b>dev</b> environment, so a Scientist's Panel Labels edits can be committed and promoted to
/// production. Not CI tests — run locally with the dev environment's secrets, by `dotnet test --filter`
/// against a single test (the Skip attribute keeps them out of normal runs).
///
/// <list type="bullet">
///   <item><b>Links</b> come from the dev Postgres — set <c>ConnectionStrings__DefaultConnection</c>
///   (or Odin.Api user-secrets) to the dev DB.</item>
///   <item><b>Labels</b> come from the dev tools-api — set <c>ToolsApi__BaseUrl</c> + <c>ToolsApi__ApiKey</c>
///   (or Odin.Api user-secrets) to the dev tools-api. The committed file's git diff is the changelog.</item>
/// </list>
/// </summary>
public class PanelPromotionSnapshotExportTests
{
    private const string Panel = "HO";

    private static readonly JsonSerializerOptions WriteOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    [Fact(Skip = "Manual seed-export utility — reads the dev Postgres; run locally only.")]
    public async Task Export_PanelSampleLinks_RewritesSeedJson()
    {
        var connectionString = ResolveConnectionString();
        Assert.False(
            string.IsNullOrWhiteSpace(connectionString),
            "ConnectionStrings:DefaultConnection must be set in Odin.Api user-secrets " +
            "or via the ConnectionStrings__DefaultConnection environment variable.");

        const string sql = """
            SELECT l."SampleId", p."Name" AS "PopulationName"
            FROM public.qpadm_population_panel_samples l
            JOIN public.qpadm_populations p ON l."QpadmPopulationId" = p."Id"
            WHERE l."Panel" = @panel
            ORDER BY l."SampleId", p."Name";
            """;

        var snapshot = new PanelLinksSnapshot { Panels = [Panel] };

        await using (var connection = new NpgsqlConnection(connectionString))
        {
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand(sql, connection);
            command.Parameters.AddWithValue("panel", Panel);
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                snapshot.Links.Add(new PanelLinkRow
                {
                    Panel = Panel,
                    SampleId = reader.GetString(0),
                    PopulationName = reader.GetString(1),
                });
            }
        }

        await File.WriteAllTextAsync(
            ResolveSeedFilePath("qpadm-population-panel-samples.json"),
            JsonSerializer.Serialize(snapshot, WriteOptions) + "\n");
    }

    [Fact(Skip = "Manual seed-export utility — reads the dev tools-api; run locally only.")]
    public async Task Export_PanelLabels_RewritesSeedJson()
    {
        var (baseUrl, apiKey) = ResolveToolsApi();
        Assert.False(
            string.IsNullOrWhiteSpace(baseUrl),
            "ToolsApi:BaseUrl must be set in Odin.Api user-secrets or via the ToolsApi__BaseUrl " +
            "environment variable (point it at the dev tools-api).");

        using var http = new HttpClient { BaseAddress = new Uri(baseUrl) };
        if (!string.IsNullOrWhiteSpace(apiKey))
            http.DefaultRequestHeaders.Add("X-Api-Key", apiKey.Trim());

        using var response = await http.GetAsync($"/v1/merge/panel/ind?panel={Panel}");
        response.EnsureSuccessStatusCode();

        var ind = await response.Content.ReadFromJsonAsync<ToolsIndResponse>(
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        Assert.NotNull(ind);
        Assert.NotNull(ind.Rows);

        var snapshot = new PanelLabelsSnapshot
        {
            Panel = Panel,
            Rows = ind.Rows.Select(r => new PanelLabelRow { Id = r.Id, Label = r.Label }).ToList(),
        };

        await File.WriteAllTextAsync(
            ResolveSeedFilePath("panel-labels-HO.json"),
            JsonSerializer.Serialize(snapshot, WriteOptions) + "\n");
    }

    private static string ResolveConnectionString()
    {
        var fromEnv = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection");
        if (!string.IsNullOrWhiteSpace(fromEnv)) return fromEnv.Trim();

        var config = new ConfigurationBuilder()
            .AddUserSecrets(typeof(ApplicationDbContext).Assembly)
            .Build();
        return config.GetConnectionString("DefaultConnection") ?? string.Empty;
    }

    private static (string BaseUrl, string ApiKey) ResolveToolsApi()
    {
        var config = new ConfigurationBuilder()
            .AddUserSecrets(typeof(ApplicationDbContext).Assembly)
            .AddEnvironmentVariables()
            .Build();
        return (config["ToolsApi:BaseUrl"] ?? string.Empty, config["ToolsApi:ApiKey"] ?? string.Empty);
    }

    private static string ResolveSeedFilePath(string fileName)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Odin.slnx")))
            dir = dir.Parent;
        if (dir is null)
            throw new InvalidOperationException(
                "Could not locate Odin.slnx — unable to resolve the seed-data file path.");
        return Path.Combine(dir.FullName, "Odin.Api", "Data", "SeedData", fileName);
    }

    private sealed record ToolsIndResponse(List<ToolsIndRow> Rows);

    private sealed record ToolsIndRow(int Index, string Id, string Sex, string Label);
}
