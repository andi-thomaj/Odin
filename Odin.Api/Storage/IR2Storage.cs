namespace Odin.Api.Storage;

/// <summary>
/// Minimal S3-compatible blob abstraction over the configured Cloudflare R2 bucket.
/// Operations target the single bucket bound to <see cref="R2Options.BucketName"/>;
/// callers pass keys (e.g. <c>populations/29.mp4</c>) without bucket prefixes.
/// </summary>
public interface IR2Storage
{
    /// <summary>
    /// Uploads (or overwrites) the object at <paramref name="key"/>. The stream is consumed
    /// in full; callers are responsible for disposing the source stream.
    /// </summary>
    Task UploadAsync(
        string key,
        Stream content,
        string contentType,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes the object at <paramref name="key"/>. A 404 from R2 is treated as success
    /// (idempotent delete) so retries after a partial failure don't surface as errors.
    /// </summary>
    Task DeleteAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Builds the public URL for <paramref name="key"/> using <see cref="R2Options.PublicBaseUrl"/>.
    /// Returned URLs are stable; cache-busting is the caller's job (e.g. <c>?v=...</c> query).
    /// </summary>
    string GetPublicUrl(string key);
}
