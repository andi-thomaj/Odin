using System.Collections.Concurrent;
using Odin.Api.Storage;

namespace Odin.Api.IntegrationTests.Fakers;

/// <summary>
/// In-memory <see cref="IR2Storage"/> for tests — the real R2Storage needs Cloudflare credentials and
/// network. Keeps uploaded bytes in a dictionary so download/delete round-trip (the image-edit flow reads
/// reference images back). Public URLs are deterministic fakes.
/// </summary>
public sealed class FakeR2Storage : IR2Storage
{
    private readonly ConcurrentDictionary<string, byte[]> _objects = new();

    public async Task UploadAsync(string key, Stream content, string contentType, CancellationToken cancellationToken = default)
    {
        using var ms = new MemoryStream();
        await content.CopyToAsync(ms, cancellationToken);
        _objects[key] = ms.ToArray();
    }

    public Task<byte[]?> DownloadAsync(string key, CancellationToken cancellationToken = default) =>
        Task.FromResult(_objects.TryGetValue(key, out var bytes) ? bytes : null);

    public Task DeleteAsync(string key, CancellationToken cancellationToken = default)
    {
        _objects.TryRemove(key, out _);
        return Task.CompletedTask;
    }

    public Task CopyAsync(string sourceKey, string destinationKey, CancellationToken cancellationToken = default)
    {
        if (_objects.TryGetValue(sourceKey, out var bytes))
            _objects[destinationKey] = bytes;
        return Task.CompletedTask;
    }

    public string GetPublicUrl(string key) => $"https://test-r2.local/{key}";
}
