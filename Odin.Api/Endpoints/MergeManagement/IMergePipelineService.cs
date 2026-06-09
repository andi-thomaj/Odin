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
    }

    /// <summary>Result of <see cref="IMergePipelineService.ConvertAsync"/>.</summary>
    public sealed record ConvertResult(string Converted23Andme, string FileName, string SourceVendor);

    /// <summary>Result of <see cref="IMergePipelineService.RunMergeAsync"/>.</summary>
    public sealed record MergeResult(string MergeId, string FileName, long SizeBytes, string Panel);

    /// <summary>Raised when the tools API returns a non-success status, preserving the status code.</summary>
    public sealed class MergePipelineException(HttpStatusCode statusCode, string detail) : Exception(detail)
    {
        public HttpStatusCode StatusCode { get; } = statusCode;
        public string Detail { get; } = detail;
    }
}
