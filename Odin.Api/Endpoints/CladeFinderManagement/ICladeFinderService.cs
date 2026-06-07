using Odin.Api.Endpoints.CladeFinderManagement.Models;

namespace Odin.Api.Endpoints.CladeFinderManagement
{
    /// <summary>
    /// Proxies a raw genetic file upload to the Python tools API (odin-tools-api) and returns the
    /// predicted Y-DNA haplogroup clade.
    /// </summary>
    public interface ICladeFinderService
    {
        Task<AnalyzeCladeContract.Response> AnalyzeAsync(
            IFormFile file, string? build, CancellationToken cancellationToken = default);
    }
}
