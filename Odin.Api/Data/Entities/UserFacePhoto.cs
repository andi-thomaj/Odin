using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Odin.Api.Data.Entities;

/// <summary>
/// One photo in a user's guided multi-angle face-capture set (captured by the iOS app via ARKit). Bytes live in
/// Cloudflare R2 under <see cref="R2Key"/> — <b>private</b>, served only through the authenticated download endpoint,
/// never a public URL, because face photos are biometric data. Only metadata is stored here. A user's "set" is simply
/// every row for their <see cref="UserId"/>; one upload batch shares a <see cref="CaptureSessionId"/>. A future
/// AI-image pipeline reads the bytes back from R2 (<c>IR2Storage.DownloadAsync(R2Key)</c>) to generate images of the
/// user. Face photos are <b>user-level</b> (keyed on the Auth0 sub), not order-scoped like the profile picture.
/// </summary>
public class UserFacePhoto : BaseEntity
{
    public int Id { get; set; }

    /// <summary>FK → <see cref="User"/>, the owner.</summary>
    public int UserId { get; set; }
    public User User { get; set; } = null!;

    /// <summary>Groups the photos uploaded together in one capture session (one replace-set POST).</summary>
    public Guid CaptureSessionId { get; set; }

    public string R2Key { get; set; } = string.Empty;
    public string OriginalFileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = "image/jpeg";
    public int Width { get; set; }
    public int Height { get; set; }
    public long ByteSize { get; set; }

    /// <summary>SHA-256 of the bytes (hex). Dedupes identical frames within a batch / across re-uploads.</summary>
    public string Sha256 { get; set; } = string.Empty;
}

public class UserFacePhotoConfiguration : IEntityTypeConfiguration<UserFacePhoto>
{
    public void Configure(EntityTypeBuilder<UserFacePhoto> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.R2Key).IsRequired().HasMaxLength(300);
        builder.Property(e => e.OriginalFileName).IsRequired().HasMaxLength(300);
        builder.Property(e => e.ContentType).IsRequired().HasMaxLength(50);
        builder.Property(e => e.Sha256).IsRequired().HasMaxLength(64);

        builder.HasOne(e => e.User)
            .WithMany()
            .HasForeignKey(e => e.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(e => e.UserId);
        builder.HasIndex(e => new { e.UserId, e.Sha256 });

        builder.ToTable("user_face_photos");
    }
}
