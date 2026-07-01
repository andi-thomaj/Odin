using Odin.Api.Endpoints.UserFacePhotoManagement.Models;

namespace Odin.Api.Endpoints.UserFacePhotoManagement;

/// <summary>
/// Per-user (identity-scoped) face-photo set: upload-as-replace-set, list, authenticated single-photo download,
/// delete one, clear all. Bytes live in R2; metadata in <c>user_face_photos</c>. The future AI-image pipeline reads
/// the set via <see cref="GetSetAsync"/> + <c>IR2Storage.DownloadAsync(R2Key)</c>.
/// </summary>
public interface IUserFacePhotoService
{
    /// <summary>Replaces the caller's entire current set with <paramref name="photos"/> (idempotent re-upload — a
    /// retried batch converges instead of doubling). Returns the new set, or a user-facing error string (400).</summary>
    Task<(FacePhotoSetContract.Response? Response, string? Error)> ReplaceSetAsync(
        IReadOnlyList<IFormFile> photos, Guid captureSessionId, string identityId, CancellationToken cancellationToken = default);

    /// <summary>The caller's whole current set (metadata only).</summary>
    Task<FacePhotoSetContract.Response> GetSetAsync(string identityId, CancellationToken cancellationToken = default);

    /// <summary>Bytes of one photo if it belongs to the caller. StatusCode is 200 / 403 (not owner) / 404 (missing).</summary>
    Task<(byte[]? Bytes, string? ContentType, int StatusCode)> GetPhotoBytesAsync(
        int id, string identityId, CancellationToken cancellationToken = default);

    /// <summary>Deletes one photo (R2 object + row). Returns 204 / 403 (not owner) / 404 (missing).</summary>
    Task<int> DeletePhotoAsync(int id, string identityId, CancellationToken cancellationToken = default);

    /// <summary>Clears the caller's whole set (R2 objects + rows). Returns the count removed.</summary>
    Task<int> ClearSetAsync(string identityId, CancellationToken cancellationToken = default);
}
