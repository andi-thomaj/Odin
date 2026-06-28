using Odin.Api.Endpoints.ImageGenerationManagement.Models;

namespace Odin.Api.Endpoints.ImageGenerationManagement;

/// <summary>
/// Reads/writes the single-row admin default image-generation settings. Reads are memory-cached
/// (invalidated on write; bypassed under the Testing environment); the row is seeded with defaults on
/// first access.
/// </summary>
public interface IImageSettingsService
{
    Task<ImageGenerationSettingsContract.Response> GetAsync(CancellationToken cancellationToken = default);

    Task<ImageGenerationSettingsContract.Response> UpdateAsync(
        ImageGenerationSettingsContract.Request request, string identityId, CancellationToken cancellationToken = default);
}
