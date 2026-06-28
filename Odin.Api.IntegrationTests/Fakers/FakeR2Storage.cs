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

    public Task<IReadOnlyList<string>> ListKeysAsync(string prefix, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<string> keys = _objects.Keys.Where(k => k.StartsWith(prefix, StringComparison.Ordinal)).ToList();
        return Task.FromResult(keys);
    }

    public Task<IReadOnlyList<string>> ListCommonPrefixesAsync(
        string prefix, string delimiter = "/", CancellationToken cancellationToken = default)
    {
        var set = new HashSet<string>(StringComparer.Ordinal);
        foreach (var key in _objects.Keys.Where(k => k.StartsWith(prefix, StringComparison.Ordinal)))
        {
            var remainder = key[prefix.Length..];
            var idx = remainder.IndexOf(delimiter, StringComparison.Ordinal);
            if (idx >= 0)
                set.Add(prefix + remainder[..(idx + delimiter.Length)]);
        }
        IReadOnlyList<string> result = set.ToList();
        return Task.FromResult(result);
    }

    public async Task DeleteManyAsync(IReadOnlyCollection<string> keys, CancellationToken cancellationToken = default)
    {
        foreach (var key in keys)
            await DeleteAsync(key, cancellationToken);
    }
}
