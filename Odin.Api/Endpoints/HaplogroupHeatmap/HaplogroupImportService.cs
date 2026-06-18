using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Odin.Api.Data;
using Odin.Api.Data.Entities;
using Odin.Api.Data.Enums;

namespace Odin.Api.Endpoints.HaplogroupHeatmap
{
    public interface IHaplogroupImportService
    {
        /// <summary>
        /// Rerunnable, idempotent import of the Y-haplogroup heatmap reference data: pulls the paginated
        /// extract from odin-tools-api and replaces <see cref="YHaplogroupSample"/>/<see cref="YHaplogroupTreeNode"/>
        /// wholesale in one transaction, recording a <see cref="HaplogroupImportRun"/>. Safe to run repeatedly
        /// (e.g. on a new AADR/YFull release) — a re-run with unchanged sources yields identical tables.
        /// Hangfire reads the queue/retry/concurrency attributes off this interface method.
        /// </summary>
        [Queue("default")]
        [AutomaticRetry(Attempts = 0)] // an admin re-runs on failure; an immediate retry usually fails the same way
        [DisableConcurrentExecution(timeoutInSeconds: 1800)] // never let two imports clobber the reference tables
        Task ImportAsync(string triggeredBy, CancellationToken cancellationToken = default);
    }

    public sealed class HaplogroupImportService(
        ApplicationDbContext dbContext,
        IHaploGeoExportClient exportClient,
        IMemoryCache cache,
        ILogger<HaplogroupImportService> logger) : IHaplogroupImportService
    {
        // The export caps a page at 20k; nodes (~67k off AADR v66) page, samples (~10k) fit in one.
        private const int PageSize = 20000;
        private const int InsertBatch = 5000;

        public async Task ImportAsync(string triggeredBy, CancellationToken cancellationToken = default)
        {
            var run = new HaplogroupImportRun
            {
                StartedAt = DateTime.UtcNow,
                Status = HaplogroupImportStatus.Running,
                TriggeredBy = string.IsNullOrWhiteSpace(triggeredBy) ? "system" : triggeredBy,
            };
            dbContext.HaplogroupImportRuns.Add(run);
            await dbContext.SaveChangesAsync(cancellationToken);

            try
            {
                var meta = await exportClient.GetMetaAsync(cancellationToken);
                var nodes = await FetchAllAsync(
                    (o, l) => exportClient.GetNodesAsync(o, l, cancellationToken), meta.NodeCount, cancellationToken);
                var samples = await FetchAllAsync(
                    (o, l) => exportClient.GetSamplesAsync(o, l, cancellationToken), meta.SampleCount, cancellationToken);

                await LoadAsync(nodes, samples, meta.DatasetVersion, cancellationToken);

                run.Status = HaplogroupImportStatus.Completed;
                run.CompletedAt = DateTime.UtcNow;
                run.DatasetVersion = meta.DatasetVersion;
                run.SampleCount = samples.Count;
                run.NodeCount = nodes.Count;
                run.UnresolvedCount = meta.UnresolvedCount;
                await dbContext.SaveChangesAsync(cancellationToken);

                // Bust the distribution cache token so every cached per-clade response is superseded.
                cache.Remove(HaplogroupCacheKeys.ImportToken);

                logger.LogInformation(
                    "Haplogroup import {RunId} completed: dataset={Dataset}, samples={Samples}, nodes={Nodes}, unresolved={Unresolved}.",
                    run.Id, meta.DatasetVersion, samples.Count, nodes.Count, meta.UnresolvedCount);
            }
            catch (Exception ex)
            {
                run.Status = HaplogroupImportStatus.Failed;
                run.CompletedAt = DateTime.UtcNow;
                run.Error = ex.Message.Length > 4000 ? ex.Message[..4000] : ex.Message;
                await dbContext.SaveChangesAsync(CancellationToken.None);
                // Don't rethrow: AutomaticRetry is 0 and the failure is recorded for the admin to re-run.
                logger.LogError(ex, "Haplogroup import {RunId} failed; reference tables left unchanged.", run.Id);
            }
        }

        private static async Task<List<T>> FetchAllAsync<T>(
            Func<int, int, Task<HaploGeoPage<T>>> fetchPage, int expectedTotal, CancellationToken cancellationToken)
        {
            var all = new List<T>(expectedTotal);
            var offset = 0;
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var page = await fetchPage(offset, PageSize);
                all.AddRange(page.Items);
                offset += PageSize;
                if (offset >= page.Total || page.Items.Count == 0)
                {
                    break;
                }
            }
            return all;
        }

        /// <summary>
        /// Replace both reference tables atomically: delete all rows, then bulk-insert the fresh extract.
        /// A full reload (rather than per-row upsert) is the simplest correct way to absorb added, changed,
        /// and removed samples across releases — and ~77k small rows is cheap. Wrapped in a transaction so a
        /// mid-load failure never leaves the heatmap half-updated.
        /// </summary>
        private async Task LoadAsync(
            List<HaploGeoNodeDto> nodes, List<HaploGeoSampleDto> samples, string datasetVersion,
            CancellationToken cancellationToken)
        {
            await using var tx = await dbContext.Database.BeginTransactionAsync(cancellationToken);

            await dbContext.YHaplogroupSamples.ExecuteDeleteAsync(cancellationToken);
            await dbContext.YHaplogroupTreeNodes.ExecuteDeleteAsync(cancellationToken);

            var autoDetect = dbContext.ChangeTracker.AutoDetectChangesEnabled;
            dbContext.ChangeTracker.AutoDetectChangesEnabled = false;
            try
            {
                await InsertBatchedAsync(
                    nodes.Select(n => new YHaplogroupTreeNode
                    {
                        Id = n.Id,
                        ParentId = n.ParentId,
                        Tmrca = n.Tmrca,
                        Formed = n.Formed,
                        Snps = n.Snps,
                        CentroidLat = n.CentroidLat,
                        CentroidLon = n.CentroidLon,
                        SubtreeSampleCount = n.SubtreeSampleCount,
                        DatasetVersion = datasetVersion,
                    }),
                    dbContext.YHaplogroupTreeNodes, cancellationToken);

                await InsertBatchedAsync(
                    samples.Select(s => new YHaplogroupSample
                    {
                        GeneticId = s.GeneticId,
                        IndividualId = s.IndividualId,
                        TreeNodeId = s.TreeNodeId,
                        YTerminal = s.YTerminal,
                        YIsogg = s.YIsogg,
                        YManual = s.YManual,
                        Latitude = s.Latitude,
                        Longitude = s.Longitude,
                        DateMeanBp = s.DateMeanBp,
                        DateSdBp = s.DateSdBp,
                        FullDate = s.FullDate,
                        Era = s.Era,
                        Layer = s.Layer,
                        Country = s.Country,
                        Locality = s.Locality,
                        GroupId = s.GroupId,
                        Sex = s.Sex,
                        Assessment = s.Assessment,
                        DatasetVersion = datasetVersion,
                    }),
                    dbContext.YHaplogroupSamples, cancellationToken);
            }
            finally
            {
                dbContext.ChangeTracker.AutoDetectChangesEnabled = autoDetect;
            }

            await tx.CommitAsync(cancellationToken);
        }

        private async Task InsertBatchedAsync<T>(
            IEnumerable<T> entities, DbSet<T> set, CancellationToken cancellationToken) where T : class
        {
            var batch = new List<T>(InsertBatch);
            foreach (var entity in entities)
            {
                batch.Add(entity);
                if (batch.Count >= InsertBatch)
                {
                    await set.AddRangeAsync(batch, cancellationToken);
                    await dbContext.SaveChangesAsync(cancellationToken);
                    dbContext.ChangeTracker.Clear();
                    batch.Clear();
                }
            }
            if (batch.Count > 0)
            {
                await set.AddRangeAsync(batch, cancellationToken);
                await dbContext.SaveChangesAsync(cancellationToken);
                dbContext.ChangeTracker.Clear();
            }
        }
    }
}
