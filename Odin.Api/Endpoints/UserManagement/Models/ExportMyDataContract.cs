namespace Odin.Api.Endpoints.UserManagement.Models;

/// <summary>
/// GDPR Article 20 (right to data portability) bundle: returns everything the API stores
/// about the calling user that they themselves provided or that was derived from their use
/// of the service. Large binary blobs (profile pictures, raw genetic file bytes, merged data,
/// report attachments) are represented by metadata + the existing download endpoint URL so
/// the bundle stays small enough to ship as JSON.
/// </summary>
public class ExportMyDataContract
{
    public class Response
    {
        public DateTime ExportedAt { get; set; }
        public string SchemaVersion { get; set; } = "1";
        public required ProfileSection Profile { get; set; }
        public List<QpadmOrderSection> QpadmOrders { get; set; } = [];
        public List<G25OrderSection> G25Orders { get; set; } = [];
        public List<RawGeneticFileSection> RawGeneticFiles { get; set; } = [];
        public List<ReportSection> Reports { get; set; } = [];
        public List<NotificationSection> Notifications { get; set; } = [];
        public List<SavedCoordinateSection> SavedCoordinates { get; set; } = [];
        public DownloadInstructions Downloads { get; set; } = new();
    }

    public class ProfileSection
    {
        public int Id { get; set; }
        public required string IdentityId { get; set; }
        public required string Email { get; set; }
        public string Username { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string MiddleName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public string? Country { get; set; }
        public string? CountryCode { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    public class QpadmOrderSection
    {
        public int Id { get; set; }
        public decimal Price { get; set; }
        public required string Status { get; set; }
        public bool HasViewedResults { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public QpadmInspectionSection? GeneticInspection { get; set; }
    }

    public class QpadmInspectionSection
    {
        public int Id { get; set; }
        public string FirstName { get; set; } = string.Empty;
        public string MiddleName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string? Gender { get; set; }
        public bool HasProfilePicture { get; set; }
        public int? RawGeneticFileId { get; set; }
        public List<int> RegionIds { get; set; } = [];
    }

    public class G25OrderSection
    {
        public int Id { get; set; }
        public decimal Price { get; set; }
        public required string Status { get; set; }
        public bool HasViewedResults { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public G25InspectionSection? GeneticInspection { get; set; }
    }

    public class G25InspectionSection
    {
        public int Id { get; set; }
        public string FirstName { get; set; } = string.Empty;
        public string MiddleName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string? Gender { get; set; }
        public bool HasProfilePicture { get; set; }
        public int? RawGeneticFileId { get; set; }
        public string? G25Coordinates { get; set; }
    }

    public class RawGeneticFileSection
    {
        public int Id { get; set; }
        public required string FileName { get; set; }
        public long FileSize { get; set; }
        public bool HasMergedData { get; set; }
        public string? MergedDataFileName { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class ReportSection
    {
        public int Id { get; set; }
        public required string Type { get; set; }
        public required string Subject { get; set; }
        public required string Description { get; set; }
        public required string Status { get; set; }
        public string? PageUrl { get; set; }
        public string? AttachmentFileName { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class NotificationSection
    {
        public int Id { get; set; }
        public required string Type { get; set; }
        public required string Title { get; set; }
        public required string Message { get; set; }
        public bool IsRead { get; set; }
        public DateTime? ReadAt { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class SavedCoordinateSection
    {
        public int Id { get; set; }
        public required string Title { get; set; }
        public required string RawInput { get; set; }
        public bool Scaling { get; set; }
        public required string AddMode { get; set; }
        public string? CustomName { get; set; }
        public required string ViewId { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    public class DownloadInstructions
    {
        public string ProfilePictureUrl { get; set; } = "/v1/api/orders/{orderId}/profile-picture";
        public string RawGeneticFileUrl { get; set; } = "/v1/api/raw-genetic-files/{id}/download";
        public string MergedDataUrl { get; set; } = "/v1/api/orders/{orderId}/merged-data/download";
        public string Note { get; set; } =
            "Large binary blobs are excluded from this JSON to keep it shippable. Use these endpoints to fetch the bytes individually.";
    }
}
