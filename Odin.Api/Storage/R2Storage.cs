using System.Net;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Options;

namespace Odin.Api.Storage;

/// <summary>
/// Cloudflare R2 implementation of <see cref="IR2Storage"/> via the AWS S3 SDK.
/// R2 quirks accounted for:
///  - <c>ForcePathStyle = true</c> (R2 doesn't support virtual-hosted style addressing).
///  - <c>AuthenticationRegion = "auto"</c> (R2 ignores region but the SDK requires one).
///  - Idempotent delete (404 is fine — object's already gone).
/// Single-bucket design: the bucket name comes from <see cref="R2Options.BucketName"/>,
/// callers only deal in keys.
/// </summary>
public sealed class R2Storage : IR2Storage, IDisposable
{
    private readonly R2Options _options;
    private readonly IAmazonS3 _client;
    private readonly ILogger<R2Storage> _logger;

    public R2Storage(IOptions<R2Options> options, ILogger<R2Storage> logger)
    {
        _options = options.Value;
        _logger = logger;

        if (string.IsNullOrWhiteSpace(_options.AccessKeyId))
            throw new InvalidOperationException("R2:AccessKeyId is not configured.");
        if (string.IsNullOrWhiteSpace(_options.SecretAccessKey))
            throw new InvalidOperationException("R2:SecretAccessKey is not configured.");
        if (string.IsNullOrWhiteSpace(_options.Endpoint))
            throw new InvalidOperationException("R2:Endpoint is not configured.");
        if (string.IsNullOrWhiteSpace(_options.BucketName))
            throw new InvalidOperationException("R2:BucketName is not configured.");
        if (string.IsNullOrWhiteSpace(_options.PublicBaseUrl))
            throw new InvalidOperationException("R2:PublicBaseUrl is not configured.");

        var credentials = new BasicAWSCredentials(_options.AccessKeyId, _options.SecretAccessKey);
        var config = new AmazonS3Config
        {
            ServiceURL = _options.Endpoint,
            ForcePathStyle = true,
            AuthenticationRegion = "auto",
            // AWS SDK v4 added default request/response integrity checksums (CRC32 / CRC64NVME
            // headers) that R2 rejects with `InvalidRequest`. Opt out so PutObject doesn't ship
            // those headers — R2's own request signing already covers integrity.
            RequestChecksumCalculation = RequestChecksumCalculation.WHEN_REQUIRED,
            ResponseChecksumValidation = ResponseChecksumValidation.WHEN_REQUIRED,
        };
        _client = new AmazonS3Client(credentials, config);
    }

    public async Task UploadAsync(
        string key,
        Stream content,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        var request = new PutObjectRequest
        {
            BucketName = _options.BucketName,
            Key = key,
            InputStream = content,
            ContentType = contentType,
            // Long cache for the avatar URL pattern. Cache-busting is handled by callers
            // adding `?v={version}` — same physical file, different URL on update.
            Headers = { CacheControl = "public, max-age=31536000, immutable" },
        };

        try
        {
            await _client.PutObjectAsync(request, cancellationToken);
        }
        catch (AmazonS3Exception ex)
        {
            _logger.LogError(ex,
                "R2 upload failed for {Bucket}/{Key} (status {Status})",
                _options.BucketName, key, ex.StatusCode);
            throw;
        }
    }

    public async Task DeleteAsync(string key, CancellationToken cancellationToken = default)
    {
        try
        {
            await _client.DeleteObjectAsync(new DeleteObjectRequest
            {
                BucketName = _options.BucketName,
                Key = key,
            }, cancellationToken);
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            _logger.LogDebug(
                "R2 object {Bucket}/{Key} already absent — delete is a no-op",
                _options.BucketName, key);
        }
    }

    public string GetPublicUrl(string key)
    {
        var baseUrl = _options.PublicBaseUrl.TrimEnd('/');
        return $"{baseUrl}/{key}";
    }

    public void Dispose() => _client.Dispose();
}
