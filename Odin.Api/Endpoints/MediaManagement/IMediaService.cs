namespace Odin.Api.Endpoints.MediaManagement;

public interface IMediaService
{
    Task<(byte[] Data, string ContentType, string FileName)?> GetMusicTrackAudioAsync(int musicTrackId);
    Task<(bool Success, string? Error)> UploadMusicTrackAudioAsync(int musicTrackId, IFormFile file, string identityId);
}
