using Odin.Api.Endpoints.ImageGenerationManagement.Models;
using Odin.Api.Pagination;

namespace Odin.Api.Endpoints.ImageGenerationManagement;

/// <summary>
/// Orchestrates image generation: resolves effective parameters (request overrides over the persisted
/// defaults), enforces the configurable guardrails, calls <see cref="IOpenAIImageClient"/>, persists the
/// job + produced images (bytes to R2, metadata to Postgres), and exposes the history + reference-image
/// management. <see cref="ProcessJobAsync"/> is the shared execution step used inline (sync) and by the
/// Hangfire worker (async).
/// </summary>
public interface IImageGenerationService
{
    /// <summary>Validate + create a Pending generation job; returns its id. Throws <see cref="ImageRequestValidationException"/> on a guardrail violation.</summary>
    Task<Guid> CreateGenerationJobAsync(
        GenerateImageContract.Request request, string identityId, CancellationToken cancellationToken = default);

    /// <summary>Validate + create a Pending edit (generate-from-references) job; returns its id.</summary>
    Task<Guid> CreateEditJobAsync(
        GenerateFromReferencesContract.Request request, string identityId, CancellationToken cancellationToken = default);

    /// <summary>Execute a Pending job to a terminal state. Idempotent (a Succeeded job is left untouched); rethrows <see cref="OpenAIImageException"/>.</summary>
    Task ProcessJobAsync(Guid jobId, CancellationToken cancellationToken = default);

    Task<ImageJobContract.Response?> GetJobAsync(Guid jobId, CancellationToken cancellationToken = default);

    Task<PageResponse<ImageJobContract.Response>> ListJobsAsync(PageRequest request, CancellationToken cancellationToken = default);

    /// <summary>Delete a job, its image rows, and the underlying R2 objects. Returns false if the job is missing.</summary>
    Task<bool> DeleteJobAsync(Guid jobId, CancellationToken cancellationToken = default);

    /// <summary>Validate + store an uploaded reference image. Returns an error string instead of the response on a bad upload.</summary>
    Task<(ReferenceImageContract.Response? Response, string? Error)> UploadReferenceImageAsync(
        IFormFile file, string identityId, CancellationToken cancellationToken = default);

    Task<ReferenceImageContract.Response?> GetReferenceImageAsync(int id, CancellationToken cancellationToken = default);

    Task<PageResponse<ReferenceImageContract.Response>> ListReferenceImagesAsync(
        PageRequest request, CancellationToken cancellationToken = default);

    Task<bool> DeleteReferenceImageAsync(int id, CancellationToken cancellationToken = default);
}
