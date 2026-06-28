using Hangfire;
using Microsoft.Extensions.Options;
using Odin.Api.Authentication;
using Odin.Api.Configuration;
using Odin.Api.Endpoints.ImageGenerationManagement.Models;
using Odin.Api.Extensions;
using Odin.Api.Pagination;

namespace Odin.Api.Endpoints.ImageGenerationManagement;

/// <summary>
/// Admin-only OpenAI image endpoints (<c>/v1/api/admin/images/*</c>): generate, generate-from-references
/// (edits), reference-image upload/management, job history/polling, default settings, and usage/cost
/// reporting. Generation is synchronous by default; <c>async: true</c> enqueues a Hangfire job and returns
/// 202 with a job id to poll (with a SignalR completion push).
/// </summary>
public static class ImageGenerationEndpoints
{
    public static void MapImageGenerationEndpoints(this IEndpointRouteBuilder app)
    {
        var endpoints = app.MapGroup("api/admin/images").RequireAuthorization("AdminOnly");

        endpoints.MapPost("/generate", Generate)
            .RequireRateLimiting("strict")
            .Produces<ImageJobContract.Response>(StatusCodes.Status200OK)
            .Produces<ImageJobContract.Response>(StatusCodes.Status202Accepted)
            .WithRequestTimeout(TimeSpan.FromMinutes(5));

        endpoints.MapPost("/generate-from-references", GenerateFromReferences)
            .RequireRateLimiting("strict")
            .Produces<ImageJobContract.Response>(StatusCodes.Status200OK)
            .Produces<ImageJobContract.Response>(StatusCodes.Status202Accepted)
            .WithRequestTimeout(TimeSpan.FromMinutes(5));

        endpoints.MapPost("/reference-images", UploadReferenceImage)
            .DisableAntiforgery()
            .RequireRateLimiting("file-upload")
            .Produces<ReferenceImageContract.Response>(StatusCodes.Status201Created)
            .WithRequestTimeout(TimeSpan.FromMinutes(5));

        endpoints.MapGet("/reference-images", ListReferenceImages)
            .RequireRateLimiting("authenticated")
            .Produces<PageResponse<ReferenceImageContract.Response>>(StatusCodes.Status200OK);

        endpoints.MapGet("/reference-images/{id:int}", GetReferenceImage)
            .RequireRateLimiting("authenticated")
            .Produces<ReferenceImageContract.Response>(StatusCodes.Status200OK);

        endpoints.MapDelete("/reference-images/{id:int}", DeleteReferenceImage)
            .RequireRateLimiting("strict")
            .Produces(StatusCodes.Status204NoContent);

        endpoints.MapGet("/jobs", ListJobs)
            .RequireRateLimiting("authenticated")
            .Produces<PageResponse<ImageJobContract.Response>>(StatusCodes.Status200OK);

        endpoints.MapGet("/jobs/{jobId:guid}", GetJob)
            .RequireRateLimiting("authenticated")
            .Produces<ImageJobContract.Response>(StatusCodes.Status200OK);

        endpoints.MapDelete("/jobs/{jobId:guid}", DeleteJob)
            .RequireRateLimiting("strict")
            .Produces(StatusCodes.Status204NoContent);

        endpoints.MapGet("/settings", GetSettings)
            .RequireRateLimiting("strict")
            .Produces<ImageGenerationSettingsContract.Response>(StatusCodes.Status200OK);

        endpoints.MapPut("/settings", UpdateSettings)
            .RequireRateLimiting("strict")
            .Produces<ImageGenerationSettingsContract.Response>(StatusCodes.Status200OK);

        endpoints.MapGet("/usage", GetUsage)
            .RequireRateLimiting("strict")
            .Produces<OpenAIUsageContract.Response>(StatusCodes.Status200OK);
    }

    private static async Task<IResult> Generate(
        GenerateImageContract.Request request,
        IImageGenerationService service,
        IBackgroundJobClient backgroundJobClient,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var problem = request.ValidateAndGetProblem();
        if (problem is not null) return problem;

        var identityId = httpContext.User.GetIdentityId() ?? string.Empty;

        Guid jobId;
        try
        {
            jobId = await service.CreateGenerationJobAsync(request, identityId, cancellationToken);
        }
        catch (ImageRequestValidationException ex)
        {
            return Results.BadRequest(new { ex.Message });
        }

        return await RunOrEnqueueAsync(service, backgroundJobClient, jobId, request.Async, cancellationToken);
    }

    private static async Task<IResult> GenerateFromReferences(
        GenerateFromReferencesContract.Request request,
        IImageGenerationService service,
        IBackgroundJobClient backgroundJobClient,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var problem = request.ValidateAndGetProblem();
        if (problem is not null) return problem;

        var identityId = httpContext.User.GetIdentityId() ?? string.Empty;

        Guid jobId;
        try
        {
            jobId = await service.CreateEditJobAsync(request, identityId, cancellationToken);
        }
        catch (ImageRequestValidationException ex)
        {
            return Results.BadRequest(new { ex.Message });
        }

        return await RunOrEnqueueAsync(service, backgroundJobClient, jobId, request.Async, cancellationToken);
    }

    private static async Task<IResult> RunOrEnqueueAsync(
        IImageGenerationService service,
        IBackgroundJobClient backgroundJobClient,
        Guid jobId,
        bool async,
        CancellationToken cancellationToken)
    {
        if (async)
        {
            backgroundJobClient.Enqueue<IImageGenerationWorker>(w => w.RunAsync(jobId, CancellationToken.None));
            var accepted = await service.GetJobAsync(jobId, cancellationToken);
            return Results.Accepted($"/v1/api/admin/images/jobs/{jobId}", accepted);
        }

        try
        {
            await service.ProcessJobAsync(jobId, cancellationToken);
        }
        catch (OpenAIImageException ex)
        {
            return MapOpenAiError(ex);
        }

        var job = await service.GetJobAsync(jobId, cancellationToken);
        return Results.Ok(job);
    }

    private static async Task<IResult> UploadReferenceImage(
        IImageGenerationService service, HttpContext httpContext, IFormFile file, CancellationToken cancellationToken)
    {
        var identityId = httpContext.User.GetIdentityId() ?? string.Empty;
        var (response, error) = await service.UploadReferenceImageAsync(file, identityId, cancellationToken);

        return error is not null
            ? Results.BadRequest(new { Message = error })
            : Results.Created($"/v1/api/admin/images/reference-images/{response!.Id}", response);
    }

    private static async Task<IResult> ListReferenceImages(
        IImageGenerationService service, int? skip, int? take, CancellationToken cancellationToken)
    {
        var page = new PageRequest(skip ?? 0, take ?? 25);
        return Results.Ok(await service.ListReferenceImagesAsync(page, cancellationToken));
    }

    private static async Task<IResult> GetReferenceImage(IImageGenerationService service, int id, CancellationToken cancellationToken)
    {
        var reference = await service.GetReferenceImageAsync(id, cancellationToken);
        return reference is null ? Results.NotFound() : Results.Ok(reference);
    }

    private static async Task<IResult> DeleteReferenceImage(IImageGenerationService service, int id, CancellationToken cancellationToken)
    {
        var deleted = await service.DeleteReferenceImageAsync(id, cancellationToken);
        return deleted ? Results.NoContent() : Results.NotFound();
    }

    private static async Task<IResult> ListJobs(
        IImageGenerationService service, int? skip, int? take, CancellationToken cancellationToken)
    {
        var page = new PageRequest(skip ?? 0, take ?? 25);
        return Results.Ok(await service.ListJobsAsync(page, cancellationToken));
    }

    private static async Task<IResult> GetJob(IImageGenerationService service, Guid jobId, CancellationToken cancellationToken)
    {
        var job = await service.GetJobAsync(jobId, cancellationToken);
        return job is null ? Results.NotFound() : Results.Ok(job);
    }

    private static async Task<IResult> DeleteJob(IImageGenerationService service, Guid jobId, CancellationToken cancellationToken)
    {
        var deleted = await service.DeleteJobAsync(jobId, cancellationToken);
        return deleted ? Results.NoContent() : Results.NotFound();
    }

    private static async Task<IResult> GetSettings(IImageSettingsService service, CancellationToken cancellationToken)
        => Results.Ok(await service.GetAsync(cancellationToken));

    private static async Task<IResult> UpdateSettings(
        ImageGenerationSettingsContract.Request request,
        IImageSettingsService service,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var problem = request.ValidateAndGetProblem();
        if (problem is not null) return problem;

        var identityId = httpContext.User.GetIdentityId() ?? string.Empty;
        return Results.Ok(await service.UpdateAsync(request, identityId, cancellationToken));
    }

    private static async Task<IResult> GetUsage(
        IOpenAIImageClient client,
        IOptions<OpenAIOptions> openAiOptions,
        DateTimeOffset? from,
        DateTimeOffset? to,
        string? bucket,
        CancellationToken cancellationToken)
    {
        var options = openAiOptions.Value;
        var start = from ?? DateTimeOffset.UtcNow.AddDays(-30);
        var bucketWidth = string.IsNullOrWhiteSpace(bucket) ? "1d" : bucket;
        var model = options.GenerationModel;

        var response = new OpenAIUsageContract.Response { Model = model, Currency = "usd" };

        // Usage (completions, filtered to the model) — near-real-time. Independent of the costs call so one
        // failing (or lagging) doesn't blank the other. Soft-fail sets OpenAiError instead of erroring the page.
        try
        {
            var usage = await client.GetCompletionsUsageAsync(model, start, to, bucketWidth, cancellationToken);
            response.UsageBuckets = usage.Buckets
                .Select(b => new OpenAIUsageContract.UsageBucket
                {
                    StartTime = b.StartTime,
                    EndTime = b.EndTime,
                    Requests = b.Requests,
                    InputTokens = b.InputTokens,
                    OutputTokens = b.OutputTokens,
                })
                .ToList();
            response.TotalRequests = usage.TotalRequests;
            response.TotalInputTokens = usage.TotalInputTokens;
            response.TotalOutputTokens = usage.TotalOutputTokens;
            // Live estimate from the (near-real-time) token counts, since OpenAI's settled $ costs lag ~a day.
            response.EstimatedCostUsd =
                (usage.TotalInputTokens / 1_000_000m * options.InputCostPer1MTokensUsd)
                + (usage.TotalOutputTokens / 1_000_000m * options.OutputCostPer1MTokensUsd);
        }
        catch (OpenAIImageException ex)
        {
            response.OpenAiError = ex.Detail;
        }

        // Settled costs (org-wide, lag ~a day).
        try
        {
            var costs = await client.GetCostsAsync(start, to, cancellationToken);
            response.CostBuckets = costs.Buckets
                .Select(b => new OpenAIUsageContract.CostBucket
                {
                    StartTime = b.StartTime,
                    EndTime = b.EndTime,
                    AmountUsd = b.AmountUsd,
                })
                .ToList();
            response.TotalCostUsd = costs.TotalAmountUsd;
            if (!string.IsNullOrWhiteSpace(costs.Currency))
                response.Currency = costs.Currency;
        }
        catch (OpenAIImageException ex)
        {
            response.OpenAiError ??= ex.Detail;
        }

        return Results.Ok(response);
    }

    private static IResult MapOpenAiError(OpenAIImageException ex)
    {
        if (ex.IsModeration)
            return Results.Problem(detail: ex.Detail, statusCode: StatusCodes.Status422UnprocessableEntity);

        return (int)ex.StatusCode switch
        {
            429 => Results.Problem(detail: ex.Detail, statusCode: StatusCodes.Status429TooManyRequests),
            503 => Results.Problem(detail: ex.Detail, statusCode: StatusCodes.Status503ServiceUnavailable),
            _ => Results.Problem(detail: ex.Detail, statusCode: StatusCodes.Status502BadGateway),
        };
    }
}
