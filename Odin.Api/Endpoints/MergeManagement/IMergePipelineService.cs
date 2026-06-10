using System.Net;

namespace Odin.Api.Endpoints.MergeManagement
{
    /// <summary>
    /// Typed proxy to the odin-tools-api <c>/v1/merge</c> endpoints: convert a raw upload to 23andMe,
    /// merge it into an AADR panel (heavy), stream the resulting bundle, and delete it. Mirrors the
    /// clade-finder proxy (shared <c>ToolsApi</c> config, snake_case JSON).
    /// </summary>
    public interface IMergePipelineService
    {
        /// <summary>Normalize a raw upload to 23andMe text. Cheap/fast; no reference data needed.</summary>
        Task<ConvertResult> ConvertAsync(byte[] raw, string fileName, CancellationToken cancellationToken = default);

        /// <summary>Merge a 23andMe file into an AADR panel. Long-running; the bundle is written to the
        /// tools-api MERGE_DIR volume under <paramref name="mergeId"/>.</summary>
        Task<MergeResult> RunMergeAsync(
            string mergeId, string converted23Andme, string? panel, string? sampleId, string sex,
            CancellationToken cancellationToken = default);

        /// <summary>Open a streaming download of a merge bundle. The caller owns the returned response
        /// and must dispose it after copying the body (so multi-GB bundles never buffer in memory).</summary>
        Task<HttpResponseMessage> OpenDownloadAsync(string mergeId, CancellationToken cancellationToken = default);

        /// <summary>Delete a merge bundle. Idempotent — succeeds even if it was already gone.</summary>
        Task DeleteAsync(string mergeId, CancellationToken cancellationToken = default);

        // ── Admin panel restore (upload a pre-built AADR panel after a crash/redeploy) ──────────
        // The "HO" panel is no longer Poseidon-provisioned; an operator uploads a packed triplet
        // (v66_2M_aadr_PUB.{geno,snp,ind}). These proxy /v1/merge/panel/restore/* on the tools-api.

        /// <summary>Report which panel files are live/staged on the tools-api volume and the panel's shape.</summary>
        Task<PanelStatusResult> GetPanelStatusAsync(string? panel, CancellationToken cancellationToken = default);

        /// <summary>Stream one pre-built panel file (<paramref name="ext"/> = geno/snp/ind) straight to
        /// the tools-api staging area. <paramref name="body"/> is the raw file stream (never buffered);
        /// <paramref name="sha256"/>, when given, is verified server-side to catch a truncated upload.</summary>
        Task<PanelUploadResult> UploadPanelFileAsync(
            string ext, string? panel, string? sha256, Stream body, CancellationToken cancellationToken = default);

        /// <summary>Validate the staged triplet and install it atomically. <paramref name="force"/> installs
        /// despite "slow but usable" warnings (transposed layout / '???' labels). Throws (422) on hard errors.</summary>
        Task<PanelActivateResult> ActivatePanelAsync(
            string? panel, bool force, CancellationToken cancellationToken = default);
    }

    /// <summary>Result of <see cref="IMergePipelineService.ConvertAsync"/>.</summary>
    public sealed record ConvertResult(string Converted23Andme, string FileName, string SourceVendor);

    /// <summary>Result of <see cref="IMergePipelineService.RunMergeAsync"/>.</summary>
    public sealed record MergeResult(string MergeId, string FileName, long SizeBytes, string Panel);

    /// <summary>Presence/size of one panel file, live and staged.</summary>
    public sealed record PanelFileStatusResult(
        string Ext, bool Present, long? SizeBytes, bool Staged, long? StagedSizeBytes);

    /// <summary>Readiness and shape of a merge panel (drives the admin UI).</summary>
    public sealed record PanelStatusResult(
        string Panel, string Prefix, bool Ready, string? Layout,
        int? NIndividuals, int? NSnps, int? NPopulationLabels, IReadOnlyList<PanelFileStatusResult> Files);

    /// <summary>Result of staging one panel file.</summary>
    public sealed record PanelUploadResult(string Panel, string Ext, long StagedSizeBytes, string Sha256);

    /// <summary>Result of installing the staged triplet.</summary>
    public sealed record PanelActivateResult(
        string Panel, bool Ready, string? Layout,
        int? NIndividuals, int? NSnps, int? NPopulationLabels, IReadOnlyList<string> Warnings);

    /// <summary>Raised when the tools API returns a non-success status, preserving the status code.</summary>
    public sealed class MergePipelineException(HttpStatusCode statusCode, string detail) : Exception(detail)
    {
        public HttpStatusCode StatusCode { get; } = statusCode;
        public string Detail { get; } = detail;
    }
}
