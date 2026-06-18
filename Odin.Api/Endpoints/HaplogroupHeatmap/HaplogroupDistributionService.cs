using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Odin.Api.Data;
using Odin.Api.Data.Enums;
using Odin.Api.Endpoints.HaplogroupHeatmap.Models;

namespace Odin.Api.Endpoints.HaplogroupHeatmap
{
    public interface IHaplogroupDistributionService
    {
        Task<HaplogroupDistributionContract.Response> GetAsync(string clade, CancellationToken cancellationToken = default);
    }

    public sealed class HaplogroupDistributionService(
        ApplicationDbContext dbContext,
        IMemoryCache cache,
        IHostEnvironment environment) : IHaplogroupDistributionService
    {
        // Per-clade distributions are read-mostly reference data; cache for the cache token's lifetime.
        private static readonly TimeSpan CacheTtl = TimeSpan.FromHours(6);
        private static readonly TimeSpan TokenTtl = TimeSpan.FromMinutes(5);

        private bool CacheEnabled => !environment.IsEnvironment("Testing");

        public async Task<HaplogroupDistributionContract.Response> GetAsync(
            string clade, CancellationToken cancellationToken = default)
        {
            clade = clade.Trim();
            var token = await GetImportTokenAsync(cancellationToken);
            var cacheKey = HaplogroupCacheKeys.Distribution(token, clade);
            if (CacheEnabled && cache.TryGetValue(cacheKey, out HaplogroupDistributionContract.Response? cached) && cached is not null)
            {
                return cached;
            }

            var response = await BuildAsync(clade, cancellationToken);

            if (CacheEnabled)
            {
                cache.Set(cacheKey, response, CacheTtl);
            }
            return response;
        }

        private async Task<HaplogroupDistributionContract.Response> BuildAsync(
            string clade, CancellationToken cancellationToken)
        {
            var response = new HaplogroupDistributionContract.Response { Clade = clade };

            var exists = await dbContext.YHaplogroupTreeNodes
                .AnyAsync(n => n.Id == clade, cancellationToken);
            if (!exists)
            {
                response.Found = false; // unknown clade (e.g. not in the imported tree) → empty distribution
                return response;
            }
            response.Found = true;

            // Anchor the whole heatmap on the **nearest named subclade** — walk up the lineage to the
            // deepest ancestor that is a recognisable named clade (e.g. a deep E-V13 sub-branch → E-V13,
            // a deep R-U106 branch → R-U106), so the data is recognisable and well-populated rather than
            // the bare top-level letter or an ultra-specific terminal. Falls back to the clade itself.
            var chain = await dbContext.Database
                .SqlQueryRaw<AncestorRow>(AncestorChainSql, clade)
                .ToListAsync(cancellationToken);
            var anchorClade = chain
                .Where(a => NamedSubclades.Contains(a.Id))
                .OrderBy(a => a.Depth) // depth 0 = the clade itself; smallest depth = most specific named ancestor
                .Select(a => a.Id)
                .FirstOrDefault() ?? clade;
            response.DisplayClade = anchorClade;

            var bins = await dbContext.Database
                .SqlQueryRaw<GeoBinRow>(BinsSql, anchorClade)
                .ToListAsync(cancellationToken);
            foreach (var b in bins)
            {
                if (string.Equals(b.Layer, "modern", StringComparison.OrdinalIgnoreCase))
                {
                    response.Modern.Add(new HaplogroupDistributionContract.GeoBin { Lat = b.Lat, Lon = b.Lon, Count = b.Count });
                    response.TotalModern += b.Count;
                }
                else
                {
                    response.Ancient.Add(new HaplogroupDistributionContract.EraBin
                    {
                        Lat = b.Lat,
                        Lon = b.Lon,
                        Era = b.Era,
                        Count = b.Count,
                    });
                    response.TotalAncient += b.Count;
                }
            }

            var countries = await dbContext.Database
                .SqlQueryRaw<CountryRow>(CountrySql, anchorClade)
                .ToListAsync(cancellationToken);
            response.ModernByCountry = countries
                .Select(c => new HaplogroupDistributionContract.CountryCount { Country = c.Country, Count = c.Count })
                .ToList();

            var migration = await dbContext.Database
                .SqlQueryRaw<MigrationRow>(MigrationSql, anchorClade)
                .ToListAsync(cancellationToken);
            response.Migration = migration
                .Select(m => new HaplogroupDistributionContract.MigrationPoint
                {
                    Clade = m.Clade,
                    Tmrca = m.Tmrca,
                    Lat = m.Lat,
                    Lon = m.Lon,
                    SampleCount = m.SampleCount,
                })
                .ToList();

            // Modern frequency choropleth: ancestor-match the clade to its most-specific available
            // frequency clade (e.g. J-Z1865 → J1 → J), then return that clade's per-country %.
            var freq = await dbContext.Database
                .SqlQueryRaw<FrequencyRow>(FrequencySql, anchorClade)
                .ToListAsync(cancellationToken);
            if (freq.Count > 0)
            {
                response.ModernFrequencyClade = freq[0].CladeNodeId;
                response.ModernFrequency = freq
                    .Select(f => new HaplogroupDistributionContract.CountryFrequency
                    {
                        Country = f.Country,
                        HcKey = f.HcKey,
                        Percentage = f.Percentage,
                        SampleSize = f.SampleSize,
                    })
                    .ToList();
            }

            return response;
        }

        private async Task<int> GetImportTokenAsync(CancellationToken cancellationToken)
        {
            if (CacheEnabled && cache.TryGetValue(HaplogroupCacheKeys.ImportToken, out int cachedToken))
            {
                return cachedToken;
            }

            var token = await dbContext.HaplogroupImportRuns
                .Where(r => r.Status == HaplogroupImportStatus.Completed)
                .OrderByDescending(r => r.Id)
                .Select(r => (int?)r.Id)
                .FirstOrDefaultAsync(cancellationToken) ?? 0;

            if (CacheEnabled)
            {
                cache.Set(HaplogroupCacheKeys.ImportToken, token, TokenTtl);
            }
            return token;
        }

        // --- Raw SQL (recursive CTEs over the imported tree). Columns are PascalCase (EF default) -> quoted.
        // {0} = clade. Tables are lowercase (ToTable), so they need no quotes.

        private const string SubtreeCte = """
            WITH RECURSIVE subtree AS (
                SELECT "Id" FROM y_haplogroup_tree_nodes WHERE "Id" = {0}
                UNION ALL
                SELECT n."Id" FROM y_haplogroup_tree_nodes n
                JOIN subtree s ON n."ParentId" = s."Id"
            )
            """;

        private const string BinsSql = SubtreeCte + """

            SELECT round(sa."Latitude")::double precision AS "Lat",
                   round(sa."Longitude")::double precision AS "Lon",
                   sa."Era"   AS "Era",
                   sa."Layer" AS "Layer",
                   count(*)::int AS "Count"
            FROM y_haplogroup_samples sa
            JOIN subtree ON sa."TreeNodeId" = subtree."Id"
            GROUP BY 1, 2, 3, 4
            """;

        private const string CountrySql = SubtreeCte + """

            SELECT sa."Country" AS "Country", count(*)::int AS "Count"
            FROM y_haplogroup_samples sa
            JOIN subtree ON sa."TreeNodeId" = subtree."Id"
            WHERE sa."Layer" = 'modern' AND sa."Country" IS NOT NULL
            GROUP BY 1
            ORDER BY 2 DESC
            """;

        private const string MigrationSql = """
            WITH RECURSIVE ancestors AS (
                SELECT "Id", "ParentId", "Tmrca", "CentroidLat", "CentroidLon", "SubtreeSampleCount"
                FROM y_haplogroup_tree_nodes WHERE "Id" = {0}
                UNION ALL
                SELECT n."Id", n."ParentId", n."Tmrca", n."CentroidLat", n."CentroidLon", n."SubtreeSampleCount"
                FROM y_haplogroup_tree_nodes n
                JOIN ancestors a ON n."Id" = a."ParentId"
            )
            SELECT "Id" AS "Clade",
                   "Tmrca" AS "Tmrca",
                   "CentroidLat" AS "Lat",
                   "CentroidLon" AS "Lon",
                   "SubtreeSampleCount" AS "SampleCount"
            FROM ancestors
            WHERE "CentroidLat" IS NOT NULL AND "CentroidLon" IS NOT NULL
            ORDER BY "Tmrca" DESC NULLS LAST
            """;

        // Walk the clade's ancestors (depth 0 = the clade), join to the frequency table, and keep only
        // the rows for the **closest** ancestor that has frequency data (the most-specific available).
        private const string FrequencySql = """
            WITH RECURSIVE ancestors AS (
                SELECT "Id", "ParentId", 0 AS depth FROM y_haplogroup_tree_nodes WHERE "Id" = {0}
                UNION ALL
                SELECT n."Id", n."ParentId", a.depth + 1
                FROM y_haplogroup_tree_nodes n JOIN ancestors a ON n."Id" = a."ParentId"
            ),
            matched AS (
                SELECT a.depth, f."Country", f."HcKey", f."CladeNodeId", f."Percentage", f."SampleSize"
                FROM ancestors a
                JOIN modern_haplogroup_frequencies f ON f."CladeNodeId" = a."Id"
            )
            SELECT "Country" AS "Country", "HcKey" AS "HcKey", "CladeNodeId" AS "CladeNodeId",
                   "Percentage" AS "Percentage", "SampleSize" AS "SampleSize"
            FROM matched
            WHERE depth = (SELECT min(depth) FROM matched)
            ORDER BY "Percentage" DESC
            """;

        // Ancestor chain (id + depth, depth 0 = the clade) — used to find the nearest named subclade.
        private const string AncestorChainSql = """
            WITH RECURSIVE ancestors AS (
                SELECT "Id", "ParentId", 0 AS depth FROM y_haplogroup_tree_nodes WHERE "Id" = {0}
                UNION ALL
                SELECT n."Id", n."ParentId", a.depth + 1
                FROM y_haplogroup_tree_nodes n JOIN ancestors a ON n."Id" = a."ParentId"
            )
            SELECT "Id" AS "Id", depth AS "Depth" FROM ancestors ORDER BY depth
            """;

        // Recognisable "named subclade" YFull node ids (resolved from significant defining SNPs across all
        // haplogroups). The heatmap anchors a clade on the nearest of these, so users see e.g. E-V13 /
        // R-M269 / J-P58 rather than the bare letter or an ultra-deep terminal. Regenerate on a YFull bump.
        private static readonly HashSet<string> NamedSubclades = new(StringComparer.OrdinalIgnoreCase)
        {
            "C", "C-M217", "C-M347", "C-M38", "C-M407", "C-M48", "C-M8", "D-M174", "D-M64.1",
            "E-M123", "E-M132", "E-M2", "E-M215", "E-M293", "E-M34", "E-M329", "E-M35", "E-M3895",
            "E-M78", "E-M84", "E-V12", "E-V13", "E-V16", "E-V22", "E-V6", "E-V65",
            "G", "G-L13", "G-L497", "G-M342", "G-M377", "G-M406", "G-P15", "G-P303", "G-U1",
            "H-M197", "H-M52", "H-M69", "H-M82",
            "I-DF29", "I-L158", "I-L161", "I-L621", "I-M223", "I-M423", "I-M436", "I-Z58", "I-Z63", "I1", "I2",
            "J-L283", "J-M102", "J-M158", "J-M205", "J-M241", "J-M410", "J-M67", "J-M68", "J-P58", "J1", "J2",
            "L", "L-L1307", "L-M27", "L-M317",
            "N", "N-L1026", "N-P43", "N-TAT", "N-VL29", "N-Z1936",
            "O-M119", "O-M122", "O-M134", "O-M268", "O-M7", "O-M95", "O-P201", "O-P49",
            "Q", "Q-L54", "Q-L56", "Q-M25", "Q-M3", "Q-M378",
            "R-DF27", "R-L21", "R-L23", "R-L51", "R-L52", "R-M198", "R-M222", "R-M269", "R-M417", "R-M458",
            "R-P312", "R-U106", "R-U152", "R-Z2103", "R-Z280", "R-Z282", "R-Z283", "R-Z93", "R1a", "R2",
            "T", "T-M70", "T-P77",
        };

        // Unmapped result types for SqlQueryRaw — matched to column aliases by name.
        private sealed class GeoBinRow
        {
            public double Lat { get; set; }
            public double Lon { get; set; }
            public string Era { get; set; } = string.Empty;
            public string Layer { get; set; } = string.Empty;
            public int Count { get; set; }
        }

        private sealed class CountryRow
        {
            public string Country { get; set; } = string.Empty;
            public int Count { get; set; }
        }

        private sealed class MigrationRow
        {
            public string Clade { get; set; } = string.Empty;
            public double? Tmrca { get; set; }
            public double Lat { get; set; }
            public double Lon { get; set; }
            public int SampleCount { get; set; }
        }

        private sealed class FrequencyRow
        {
            public string Country { get; set; } = string.Empty;
            public string HcKey { get; set; } = string.Empty;
            public string CladeNodeId { get; set; } = string.Empty;
            public double Percentage { get; set; }
            public int SampleSize { get; set; }
        }

        private sealed class AncestorRow
        {
            public string Id { get; set; } = string.Empty;
            public int Depth { get; set; }
        }
    }
}
