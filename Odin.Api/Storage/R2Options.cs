namespace Odin.Api.Storage;

/// <summary>
/// Configuration for the Cloudflare R2 bucket that hosts admin-uploaded media (population
/// MP4 avatars, currently). Bound from the <c>R2</c> section of configuration.
/// Secret values come from user-secrets locally and Coolify environment variables in
/// production (never appsettings.json).
/// </summary>
public class R2Options
{
    public const string SectionName = "R2";

    /// <summary>R2 API token's access key id (`Cloudflare Dashboard → R2 → Manage R2 API Tokens`).</summary>
    public string AccessKeyId { get; set; } = string.Empty;

    /// <summary>R2 API token's secret access key. Never log or echo this value.</summary>
    public string SecretAccessKey { get; set; } = string.Empty;

    /// <summary>S3-compatible endpoint, e.g. <c>https://&lt;account-id&gt;.r2.cloudflarestorage.com</c>.</summary>
    public string Endpoint { get; set; } = string.Empty;

    /// <summary>Bucket name, e.g. <c>ancestrify</c>. Single bucket shared by every service; each
    /// service uses a distinct top-level key prefix so concerns don't collide. Current layout:
    /// <c>qpAdm/population-videos/{id}.mp4</c> and <c>qpAdm/population-music-tracks/{filename}.wav</c>;
    /// G25 keys live under their own root prefixes when added.</summary>
    public string BucketName { get; set; } = string.Empty;

    /// <summary>
    /// Public-facing base URL via the bucket's custom domain, e.g. <c>https://storage.ancestrify.io</c>.
    /// Used to build the URLs the frontend points <c>&lt;video src&gt;</c> at.
    /// </summary>
    public string PublicBaseUrl { get; set; } = string.Empty;
}
