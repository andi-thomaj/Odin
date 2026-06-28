using System.Net;
using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Odin.Api.Configuration;
using Odin.Api.Data;
using Odin.Api.Data.Entities;
using Odin.Api.Data.Enums;
using Odin.Api.Endpoints.ImageGenerationManagement.Models;
using Odin.Api.Hubs;
using Odin.Api.Pagination;
using Odin.Api.Services;
using Odin.Api.Storage;

namespace Odin.Api.Endpoints.ImageGenerationManagement;

public sealed class ImageGenerationService(
    ApplicationDbContext dbContext,
    IOpenAIImageClient openAiClient,
    IImageSettingsService settingsService,
    IR2Storage r2Storage,
    IImageGenerationRealtimeNotifier notifier,
    IOptions<ImageGenerationLimitsOptions> limitsOptions,
    ILogger<ImageGenerationService> logger) : IImageGenerationService
{
    private readonly ImageGenerationLimitsOptions _limits = limitsOptions.Value;

    public async Task<Guid> CreateGenerationJobAsync(
        GenerateImageContract.Request request, string identityId, CancellationToken cancellationToken = default)
    {
        var p = await ResolveParametersAsync(
            request.N, request.Size, request.Quality, request.Background,
            request.OutputFormat, request.OutputCompression, request.Moderation, identityId, cancellationToken);

        var now = DateTime.UtcNow;
        var job = new ImageGenerationJob
        {
            Id = Guid.NewGuid(),
            Mode = ImageGenerationMode.Generation,
            Status = ImageGenerationStatus.Pending,
            IsAsync = request.Async,
            Prompt = request.Prompt,
            Model = p.Model,
            Size = p.Size,
            Quality = p.Quality,
            Background = p.Background,
            OutputFormat = p.OutputFormat,
            OutputCompression = p.OutputCompression,
            Moderation = p.Moderation,
            N = p.N,
            CreatedBy = identityId,
            CreatedAt = now,
            UpdatedAt = now,
        };

        dbContext.ImageGenerationJobs.Add(job);
        await dbContext.SaveChangesAsync(cancellationToken);
        return job.Id;
    }

    public async Task<Guid> CreateEditJobAsync(
        GenerateFromReferencesContract.Request request, string identityId, CancellationToken cancellationToken = default)
    {
        var ids = request.ReferenceImageIds.Distinct().ToList();
        if (ids.Count > _limits.MaxReferenceImagesPerRequest)
        {
            throw new ImageRequestValidationException(
                $"Too many reference images ({ids.Count}); the maximum is {_limits.MaxReferenceImagesPerRequest}.");
        }

        var existing = await dbContext.ReferenceImages
            .Where(r => ids.Contains(r.Id))
            .Select(r => r.Id)
            .ToListAsync(cancellationToken);
        var missing = ids.Except(existing).ToList();
        if (missing.Count > 0)
            throw new ImageRequestValidationException($"Reference image(s) not found: {string.Join(", ", missing)}.");

        if (request.MaskReferenceImageId is { } maskId
            && !await dbContext.ReferenceImages.AnyAsync(r => r.Id == maskId, cancellationToken))
        {
            throw new ImageRequestValidationException($"Mask reference image {maskId} not found.");
        }

        var p = await ResolveParametersAsync(
            request.N, request.Size, request.Quality, request.Background,
            request.OutputFormat, request.OutputCompression, request.Moderation, identityId, cancellationToken);

        var now = DateTime.UtcNow;
        var job = new ImageGenerationJob
        {
            Id = Guid.NewGuid(),
            Mode = ImageGenerationMode.Edit,
            Status = ImageGenerationStatus.Pending,
            IsAsync = request.Async,
            Prompt = request.Prompt,
            Model = p.Model,
            Size = p.Size,
            Quality = p.Quality,
            Background = p.Background,
            OutputFormat = p.OutputFormat,
            OutputCompression = p.OutputCompression,
            Moderation = p.Moderation,
            N = p.N,
            ReferenceImageIds = ids.ToArray(),
            MaskReferenceImageId = request.MaskReferenceImageId,
            InputFidelity = request.InputFidelity,
            CreatedBy = identityId,
            CreatedAt = now,
            UpdatedAt = now,
        };

        dbContext.ImageGenerationJobs.Add(job);
        await dbContext.SaveChangesAsync(cancellationToken);
        return job.Id;
    }

    public async Task ProcessJobAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        var job = await dbContext.ImageGenerationJobs.FirstOrDefaultAsync(j => j.Id == jobId, cancellationToken);
        if (job is null)
        {
            logger.LogWarning("ProcessJobAsync called for unknown image job {JobId}.", jobId);
            return;
        }

        if (job.Status == ImageGenerationStatus.Succeeded)
            return; // idempotent — already done

        job.Status = ImageGenerationStatus.Running;
        job.StartedAt = DateTime.UtcNow;
        job.UpdatedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);

        try
        {
            var parameters = new OpenAIImageParameters(
                job.Model, job.Size, job.Quality, job.Background, job.OutputFormat,
                job.OutputCompression, job.Moderation, job.N, job.CreatedBy);

            var result = job.Mode == ImageGenerationMode.Edit
                ? await RunEditAsync(job, parameters, cancellationToken)
                : await openAiClient.GenerateAsync(new OpenAIGenerateRequest(job.Prompt, parameters), cancellationToken);

            var (contentType, ext) = ResolveContentType(job.OutputFormat);
            var images = new List<GeneratedImage>(result.Images.Count);
            var now = DateTime.UtcNow;
            for (var i = 0; i < result.Images.Count; i++)
            {
                var bytes = result.Images[i].Bytes;
                var key = $"openai/images/{job.Id:N}/{i}.{ext}";
                using (var stream = new MemoryStream(bytes, writable: false))
                {
                    await r2Storage.UploadAsync(key, stream, contentType, cancellationToken);
                }

                images.Add(new GeneratedImage
                {
                    JobId = job.Id,
                    BatchIndex = i,
                    R2Key = key,
                    ContentType = contentType,
                    FileSizeBytes = bytes.LongLength,
                    CreatedBy = job.CreatedBy,
                    CreatedAt = now,
                    UpdatedAt = now,
                });
            }

            dbContext.GeneratedImages.AddRange(images);

            job.Status = ImageGenerationStatus.Succeeded;
            job.RevisedPrompt = result.Images.FirstOrDefault()?.RevisedPrompt;
            job.UsageInputTokens = result.Usage?.InputTokens;
            job.UsageOutputTokens = result.Usage?.OutputTokens;
            job.UsageTotalTokens = result.Usage?.TotalTokens;
            job.CompletedAt = DateTime.UtcNow;
            job.UpdatedAt = DateTime.UtcNow;
            await dbContext.SaveChangesAsync(cancellationToken);

            logger.LogInformation("Image job {JobId} succeeded with {Count} image(s).", job.Id, images.Count);
            await notifier.NotifyUsageChangedAsync(cancellationToken);
        }
        catch (OpenAIImageException ex)
        {
            await RecordFailureAsync(job, ex.Code ?? "openai_error", ex.Detail, cancellationToken);
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Image job {JobId} failed unexpectedly.", job.Id);
            await RecordFailureAsync(job, "internal_error", "Image generation failed unexpectedly.", cancellationToken);
            throw;
        }
    }

    private async Task<OpenAIImageResult> RunEditAsync(
        ImageGenerationJob job, OpenAIImageParameters parameters, CancellationToken cancellationToken)
    {
        var ids = job.ReferenceImageIds ?? [];
        var refs = await dbContext.ReferenceImages
            .Where(r => ids.Contains(r.Id))
            .ToListAsync(cancellationToken);
        var byId = refs.ToDictionary(r => r.Id);

        var images = new List<OpenAIReferenceImage>(ids.Length);
        foreach (var id in ids) // preserve request order
        {
            if (!byId.TryGetValue(id, out var reference))
                throw new OpenAIImageException(HttpStatusCode.UnprocessableEntity, $"Reference image {id} no longer exists.");

            var bytes = await r2Storage.DownloadAsync(reference.R2Key, cancellationToken)
                ?? throw new OpenAIImageException(HttpStatusCode.UnprocessableEntity, $"Reference image {id} is no longer available in storage.");
            images.Add(new OpenAIReferenceImage(bytes, reference.ContentType, reference.OriginalFileName));
        }

        OpenAIReferenceImage? mask = null;
        if (job.MaskReferenceImageId is { } maskId)
        {
            var maskRef = await dbContext.ReferenceImages.FirstOrDefaultAsync(r => r.Id == maskId, cancellationToken);
            var maskBytes = maskRef is null ? null : await r2Storage.DownloadAsync(maskRef.R2Key, cancellationToken);
            if (maskRef is not null && maskBytes is not null)
                mask = new OpenAIReferenceImage(maskBytes, maskRef.ContentType, maskRef.OriginalFileName);
        }

        return await openAiClient.EditAsync(
            new OpenAIEditRequest(job.Prompt, parameters, images, mask, job.InputFidelity), cancellationToken);
    }

    private async Task RecordFailureAsync(ImageGenerationJob job, string code, string message, CancellationToken cancellationToken)
    {
        job.Status = ImageGenerationStatus.Failed;
        job.ErrorCode = Truncate(code, 100);
        job.ErrorMessage = Truncate(message, 2000);
        job.CompletedAt = DateTime.UtcNow;
        job.UpdatedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        await notifier.NotifyUsageChangedAsync(cancellationToken);
    }

    public async Task<ImageJobContract.Response?> GetJobAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        var job = await dbContext.ImageGenerationJobs
            .AsNoTracking()
            .Include(j => j.Images)
            .FirstOrDefaultAsync(j => j.Id == jobId, cancellationToken);
        return job is null ? null : MapJob(job);
    }

    public async Task<PageResponse<ImageJobContract.Response>> ListJobsAsync(
        PageRequest request, CancellationToken cancellationToken = default)
    {
        var req = request.Sanitized();
        var query = dbContext.ImageGenerationJobs.AsNoTracking().Include(j => j.Images);
        var total = await query.CountAsync(cancellationToken);
        var jobs = await query
            .OrderByDescending(j => j.CreatedAt)
            .Skip(req.Skip)
            .Take(req.Take)
            .ToListAsync(cancellationToken);

        return new PageResponse<ImageJobContract.Response>(jobs.Select(MapJob).ToList(), total, req.Skip, req.Take);
    }

    public async Task<bool> DeleteJobAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        var job = await dbContext.ImageGenerationJobs
            .Include(j => j.Images)
            .FirstOrDefaultAsync(j => j.Id == jobId, cancellationToken);
        if (job is null)
            return false;

        foreach (var image in job.Images)
            await r2Storage.DeleteAsync(image.R2Key, cancellationToken);

        dbContext.ImageGenerationJobs.Remove(job); // cascades to generated_images
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<(ReferenceImageContract.Response? Response, string? Error)> UploadReferenceImageAsync(
        IFormFile file, string identityId, CancellationToken cancellationToken = default)
    {
        if (file.Length == 0)
            return (null, "The uploaded file is empty.");
        if (file.Length > _limits.MaxReferenceUploadBytes)
            return (null, $"The file exceeds the maximum size of {_limits.MaxReferenceUploadBytes} bytes.");

        using var ms = new MemoryStream();
        await file.CopyToAsync(ms, cancellationToken);
        var bytes = ms.ToArray();

        if (!FileSignatureValidator.IsSupportedImage(bytes, out var contentType))
            return (null, "Unsupported image. Allowed types: PNG, JPEG, WEBP.");

        var ext = ExtFromContentType(contentType);
        var sha = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        var key = $"openai/reference-images/{Guid.NewGuid():N}.{ext}";

        using (var upload = new MemoryStream(bytes, writable: false))
        {
            await r2Storage.UploadAsync(key, upload, contentType, cancellationToken);
        }

        var now = DateTime.UtcNow;
        var entity = new ReferenceImage
        {
            OriginalFileName = SafeFileName(file.FileName),
            R2Key = key,
            ContentType = contentType,
            FileSizeBytes = bytes.LongLength,
            Sha256 = sha,
            CreatedBy = identityId,
            CreatedAt = now,
            UpdatedAt = now,
        };
        dbContext.ReferenceImages.Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken);

        return (MapReference(entity), null);
    }

    public async Task<ReferenceImageContract.Response?> GetReferenceImageAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.ReferenceImages.AsNoTracking().FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
        return entity is null ? null : MapReference(entity);
    }

    public async Task<PageResponse<ReferenceImageContract.Response>> ListReferenceImagesAsync(
        PageRequest request, CancellationToken cancellationToken = default)
    {
        var req = request.Sanitized();
        var query = dbContext.ReferenceImages.AsNoTracking();
        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(r => r.CreatedAt)
            .Skip(req.Skip)
            .Take(req.Take)
            .ToListAsync(cancellationToken);

        return new PageResponse<ReferenceImageContract.Response>(
            items.Select(MapReference).ToList(), total, req.Skip, req.Take);
    }

    public async Task<bool> DeleteReferenceImageAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.ReferenceImages.FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
        if (entity is null)
            return false;

        await r2Storage.DeleteAsync(entity.R2Key, cancellationToken);
        dbContext.ReferenceImages.Remove(entity);
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    private async Task<OpenAIImageParameters> ResolveParametersAsync(
        int? n, string? size, string? quality, string? background, string? outputFormat,
        int? outputCompression, string? moderation, string identityId, CancellationToken cancellationToken)
    {
        var defaults = await settingsService.GetAsync(cancellationToken);

        var resolvedN = n ?? defaults.DefaultN;
        if (resolvedN < 1) resolvedN = 1;
        if (resolvedN > _limits.MaxImagesPerRequest)
            throw new ImageRequestValidationException($"N ({resolvedN}) exceeds the configured maximum of {_limits.MaxImagesPerRequest}.");

        var size_ = size ?? defaults.Size;
        var quality_ = quality ?? defaults.Quality;
        var background_ = background ?? defaults.Background;
        var format_ = outputFormat ?? defaults.OutputFormat;
        var moderation_ = moderation ?? defaults.Moderation;
        var compression_ = outputCompression ?? defaults.OutputCompression;

        EnsureValidSize(size_);
        EnsureAllowed(_limits.AllowedQualities, quality_, "quality");
        EnsureAllowed(_limits.AllowedBackgrounds, background_, "background");
        EnsureAllowed(_limits.AllowedOutputFormats, format_, "output format");
        EnsureAllowed(_limits.AllowedModerationLevels, moderation_, "moderation");

        return new OpenAIImageParameters(
            defaults.Model, size_, quality_, background_, format_, compression_, moderation_, resolvedN, identityId);
    }

    private static void EnsureAllowed(string[] allowed, string value, string label)
    {
        if (!ImageParameterVocabulary.IsValid(allowed, value))
        {
            throw new ImageRequestValidationException(
                $"The {label} '{value}' is not allowed. Allowed: {string.Join(", ", allowed)}.");
        }
    }

    private void EnsureValidSize(string size)
    {
        // gpt-image-2 hard constraints (the resolved size may come from persisted defaults, not the request).
        if (ImageParameterVocabulary.ValidateSize(size) is { } error)
            throw new ImageRequestValidationException(error);

        // Configurable cost cap on total pixels for a custom dimension.
        if (ImageParameterVocabulary.TryParseDimensions(size, out var width, out var height)
            && (long)width * height > _limits.MaxImagePixels)
        {
            throw new ImageRequestValidationException(
                $"The requested size {size} exceeds the configured maximum of {_limits.MaxImagePixels:N0} pixels.");
        }
    }

    private ImageJobContract.Response MapJob(ImageGenerationJob job) => new()
    {
        JobId = job.Id,
        Mode = job.Mode.ToString(),
        Status = job.Status.ToString(),
        IsAsync = job.IsAsync,
        Prompt = job.Prompt,
        RevisedPrompt = job.RevisedPrompt,
        Model = job.Model,
        Size = job.Size,
        Quality = job.Quality,
        Background = job.Background,
        OutputFormat = job.OutputFormat,
        OutputCompression = job.OutputCompression,
        Moderation = job.Moderation,
        N = job.N,
        ReferenceImageIds = job.ReferenceImageIds,
        UsageInputTokens = job.UsageInputTokens,
        UsageOutputTokens = job.UsageOutputTokens,
        UsageTotalTokens = job.UsageTotalTokens,
        ErrorCode = job.ErrorCode,
        ErrorMessage = job.ErrorMessage,
        CreatedAt = job.CreatedAt,
        CompletedAt = job.CompletedAt,
        Images = job.Images
            .OrderBy(i => i.BatchIndex)
            .Select(i => new ImageJobContract.Response.Image
            {
                Id = i.Id,
                BatchIndex = i.BatchIndex,
                Url = r2Storage.GetPublicUrl(i.R2Key),
                ContentType = i.ContentType,
                FileSizeBytes = i.FileSizeBytes,
            })
            .ToList(),
    };

    private ReferenceImageContract.Response MapReference(ReferenceImage r) => new()
    {
        Id = r.Id,
        OriginalFileName = r.OriginalFileName,
        Url = r2Storage.GetPublicUrl(r.R2Key),
        ContentType = r.ContentType,
        FileSizeBytes = r.FileSizeBytes,
        Sha256 = r.Sha256,
        CreatedAt = r.CreatedAt,
    };

    private static (string ContentType, string Extension) ResolveContentType(string outputFormat) => outputFormat switch
    {
        "jpeg" => ("image/jpeg", "jpg"),
        "webp" => ("image/webp", "webp"),
        _ => ("image/png", "png"),
    };

    private static string ExtFromContentType(string contentType) => contentType switch
    {
        "image/jpeg" => "jpg",
        "image/webp" => "webp",
        _ => "png",
    };

    private static string SafeFileName(string? fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            return "reference-image";
        var trimmed = fileName.Trim();
        return trimmed.Length > 300 ? trimmed[^300..] : trimmed;
    }

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength];
}
