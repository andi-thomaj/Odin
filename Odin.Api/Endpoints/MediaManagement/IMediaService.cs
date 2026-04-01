namespace Odin.Api.Endpoints.MediaManagement;

public interface IMediaService
{
    Task<(byte[] Data, string ContentType, string FileName)?> GetMusicTrackAudioAsync(int musicTrackId);
    Task<(byte[] Data, string ContentType, string FileName)?> GetPopulationVideoAsync(int populationId);
    Task<(bool Success, string? Error)> UploadMusicTrackAudioAsync(int musicTrackId, IFormFile file, string identityId);
    Task<(bool Success, string? Error)> UploadPopulationVideoAsync(int populationId, IFormFile file, string identityId);
}
