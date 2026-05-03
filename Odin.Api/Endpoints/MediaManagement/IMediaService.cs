namespace Odin.Api.Endpoints.MediaManagement;

public interface IMediaService
{
    Task<(byte[] Data, string ContentType, string FileName)?> GetMusicTrackAudioAsync(int musicTrackId);
    Task<(bool Success, string? Error)> UploadMusicTrackAudioAsync(int musicTrackId, IFormFile file, string identityId);

    /// <summary>
    /// Mirrors the per-population <c>SyncVideoAvatarsFromDiskAsync</c> for music tracks: for each
    /// <c>MusicTrack</c> in the database, looks up <c>Data/SeedData/media/audio/{FileName}</c> and
    /// uploads it to R2 under the stable key <c>qpAdm/population-music-tracks/{FileName}</c>.
    /// Idempotent — re-running overwrites the R2 object. <paramref name="FirstError"/> carries the
    /// first failure's <see cref="Exception.Message"/> so the admin sees the underlying R2/auth/bucket
    /// problem in the response without having to read the server logs.
    /// </summary>
    Task<(int Updated, int MissingOnDisk, int Failed, int Unmatched, string? FirstError)> SyncMusicTrackAudioFromDiskAsync(
        string identityId, CancellationToken cancellationToken = default);
}
