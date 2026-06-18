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

            var bins = await dbContext.Database
                .SqlQueryRaw<GeoBinRow>(BinsSql, clade)
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
                .SqlQueryRaw<CountryRow>(CountrySql, clade)
                .ToListAsync(cancellationToken);
            response.ModernByCountry = countries
                .Select(c => new HaplogroupDistributionContract.CountryCount { Country = c.Country, Count = c.Count })
                .ToList();

            var migration = await dbContext.Database
                .SqlQueryRaw<MigrationRow>(MigrationSql, clade)
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
    }
}
