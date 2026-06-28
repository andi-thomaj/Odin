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
    /// Downloads the object at <paramref name="key"/> in full and returns its bytes, or <c>null</c> if
    /// the object does not exist (a 404 is not an error). Intended for small objects (e.g. reference
    /// images re-read for an OpenAI edit), not multi-GB blobs.
    /// </summary>
    Task<byte[]?> DownloadAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Server-side copy from <paramref name="sourceKey"/> to <paramref name="destinationKey"/>
    /// within the configured bucket. Used when a name-keyed object needs to follow a rename
    /// without re-uploading the bytes. The source object is left intact; pair with
    /// <see cref="DeleteAsync"/> for a "move".
    /// </summary>
    Task CopyAsync(string sourceKey, string destinationKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// Builds the public URL for <paramref name="key"/> using <see cref="R2Options.PublicBaseUrl"/>.
    /// Returned URLs are stable; cache-busting is the caller's job (e.g. <c>?v=...</c> query).
    /// </summary>
    string GetPublicUrl(string key);
}
