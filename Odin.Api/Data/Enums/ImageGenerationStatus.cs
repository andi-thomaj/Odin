namespace Odin.Api.Data.Enums;

/// <summary>
/// Lifecycle of an image-generation job. A synchronous request transitions Pending → Running →
/// Succeeded/Failed inline; an async (Hangfire) request is created Pending, picked up by a worker, and
/// pushed to the same terminal states. Stored as a string (<c>HasConversion&lt;string&gt;</c>) so adding a
/// value needs no data migration.
/// </summary>
public enum ImageGenerationStatus
{
    /// <summary>Job row created; OpenAI has not been called yet (the queued state for async jobs).</summary>
    Pending,

    /// <summary>The OpenAI call is in progress.</summary>
    Running,

    /// <summary>Images were produced, uploaded to R2, and persisted.</summary>
    Succeeded,

    /// <summary>The generation failed terminally (moderation block, bad request, or exhausted retries).</summary>
    Failed,
}
