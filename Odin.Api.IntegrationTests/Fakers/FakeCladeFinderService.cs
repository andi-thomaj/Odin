using System.Net;
using Microsoft.AspNetCore.Http;
using Odin.Api.Endpoints.CladeFinderManagement;
using Odin.Api.Endpoints.CladeFinderManagement.Models;

namespace Odin.Api.IntegrationTests.Fakers;

/// <summary>
/// Test double for <see cref="ICladeFinderService"/> — the real one calls the external Python
/// tools API, which isn't available in tests. Returns a canned clade for normal uploads, and
/// throws <see cref="CladeFinderException"/> for sentinel filenames so the endpoint's error
/// mapping (503 / 400 / 502) can be exercised.
/// </summary>
public sealed class FakeCladeFinderService : ICladeFinderService
{
    public Task<AnalyzeCladeContract.Response> AnalyzeAsync(
        IFormFile file, string? build, CancellationToken cancellationToken = default)
    {
        var name = file.FileName ?? string.Empty;

        if (name.Contains("boom-503", StringComparison.OrdinalIgnoreCase))
            throw new CladeFinderException(HttpStatusCode.ServiceUnavailable, "reference data not configured");

        if (name.Contains("boom-400", StringComparison.OrdinalIgnoreCase))
            throw new CladeFinderException(HttpStatusCode.BadRequest, "no Y-chromosome SNP calls found");

        return Task.FromResult(new AnalyzeCladeContract.Response
        {
            Clade = "A-B-C",
            Score = 6.0,
            NextPrediction = new AnalyzeCladeContract.NextPrediction { Clade = "A-B", Score = 4.0 },
            Downstream = [],
            PositivesUsed = 6,
            NegativesUsed = 3,
            SourceFormat = name.EndsWith(".vcf", StringComparison.OrdinalIgnoreCase) ? "vcf" : "microarray",
        });
    }
}
