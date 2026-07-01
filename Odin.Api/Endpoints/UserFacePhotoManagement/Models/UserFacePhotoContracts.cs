namespace Odin.Api.Endpoints.UserFacePhotoManagement.Models;

/// <summary>Server-enforced limits for a user's face-photo set. The total stays under the global 50 MB request cap.</summary>
public static class UserFacePhotoLimits
{
    public const int MaxPhotos = 20;
    public const long MaxPhotoBytes = 2_621_440;          // 2.5 MB per file
    public const long MaxTotalBytes = 40L * 1024 * 1024;  // 40 MB per set (under the global 50 MB request cap)
}

/// <summary>One photo's metadata. The bytes are NOT exposed via a public R2 URL (biometric data) — fetch them from
/// the authenticated <see cref="DownloadUrl"/> (relative path) with a bearer token.</summary>
public static class FacePhotoContract
{
    public class Response
    {
        public int Id { get; set; }
        public Guid CaptureSessionId { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public long ByteSize { get; set; }
        public string Sha256 { get; set; } = string.Empty;
        /// <summary>Authenticated, relative download path (e.g. <c>/v1/api/users/face-photos/42/download</c>).</summary>
        public string DownloadUrl { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
    }
}

/// <summary>The caller's whole current face-photo set (all rows for their identity).</summary>
public static class FacePhotoSetContract
{
    public class Response
    {
        public Guid? CaptureSessionId { get; set; }
        public int Count { get; set; }
        public List<FacePhotoContract.Response> Photos { get; set; } = [];
    }
}
