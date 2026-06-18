using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Odin.Api.Data;
using Odin.Api.Data.Enums;
using Odin.Api.Endpoints.HaplogroupHeatmap.Models;

namespace Odin.Api.Endpoints.HaplogroupHeatmap
{
    public interface IHaplogroupRelativeFrequencyService
    {
        Task<RelativeFrequencyContract.Response> GetAsync(
            string clade, string layer, double radiusKm, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Serves the relative-frequency surface (the heatmap's 3rd mode): checks the clade exists in the
    /// imported tree, anchors it to its nearest named subclade (shared <see cref="HaplogroupAnchor"/>),
    /// and proxies the grid compute to odin-tools-api — caching per (import token, anchored clade, layer,
    /// radius) so a fresh import busts every cached surface, exactly like the distribution endpoint.
    /// </summary>
    public sealed class HaplogroupRelativeFrequencyService(
        ApplicationDbContext dbContext,
        IHaplogroupRelativeFrequencyClient client,
        IMemoryCache cache,
        IHostEnvironment environment) : IHaplogroupRelativeFrequencyService
    {
        private static readonly TimeSpan CacheTtl = TimeSpan.FromHours(6);
        private static readonly TimeSpan TokenTtl = TimeSpan.FromMinutes(5);
        private const double MinRadiusKm = 50.0;
        private const double MaxRadiusKm = 2000.0;

        private bool CacheEnabled => !environment.IsEnvironment("Testing");

        public async Task<RelativeFrequencyContract.Response> GetAsync(
            string clade, string layer, double radiusKm, CancellationToken cancellationToken = default)
        {
            clade = clade.Trim();
            layer = string.Equals(layer, "modern", StringComparison.OrdinalIgnoreCase) ? "modern" : "ancient";
            radiusKm = Math.Clamp(radiusKm, MinRadiusKm, MaxRadiusKm);

            var token = await GetImportTokenAsync(cancellationToken);
            var cacheKey = HaplogroupCacheKeys.RelativeFrequency(token, clade, layer, radiusKm);
            if (CacheEnabled &&
                cache.TryGetValue(cacheKey, out RelativeFrequencyContract.Response? cached) && cached is not null)
            {
                return cached;
            }

            var response = await BuildAsync(clade, layer, radiusKm, cancellationToken);

            if (CacheEnabled)
            {
                cache.Set(cacheKey, response, CacheTtl);
            }
            return response;
        }

        private async Task<RelativeFrequencyContract.Response> BuildAsync(
            string clade, string layer, double radiusKm, CancellationToken cancellationToken)
        {
            var response = new RelativeFrequencyContract.Response
            {
                Clade = clade,
                DisplayClade = clade,
                Layer = layer,
                RadiusKm = radiusKm,
            };

            var exists = await dbContext.YHaplogroupTreeNodes
                .AnyAsync(n => n.Id == clade, cancellationToken);
            if (!exists)
            {
                response.Found = false; // unknown clade → empty surface (mirrors the distribution endpoint)
                return response;
            }
            response.Found = true;

            var anchor = await HaplogroupAnchor.ResolveAsync(dbContext, clade, cancellationToken);
            response.DisplayClade = anchor;

            // The grid itself is computed by odin-tools-api from the same data we imported.
            var grid = await client.GetAsync(anchor, layer, radiusKm, cancellationToken);
            response.CellSize = grid.CellSize;
            response.FrequencyClade = grid.FrequencyClade;
            response.MaxValue = grid.MaxValue;
            response.CladeCount = grid.CladeCount;
            response.TotalCount = grid.TotalCount;
            response.Cells = grid.Cells
                .Select(c => new RelativeFrequencyContract.Cell { Lat = c.Lat, Lon = c.Lon, Value = c.Value })
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
    }
}
