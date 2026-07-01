using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Odin.Api.Data.Entities;

/// <summary>
/// An admin-uploaded image kept so it can be referenced by later edit ("generate from references")
/// requests. Bytes live in Cloudflare R2 under <see cref="R2Key"/>; only metadata is stored here. The
/// edit flow streams the bytes back from R2 into OpenAI's <c>/v1/images/edits</c> call.
/// </summary>
public class ReferenceImage : BaseEntity
{
    public int Id { get; set; }

    public string OriginalFileName { get; set; } = string.Empty;
    public string R2Key { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }

    /// <summary>SHA-256 of the bytes (hex). Lets the UI dedupe / spot re-uploads.</summary>
    public string? Sha256 { get; set; }
}

public class ReferenceImageConfiguration : IEntityTypeConfiguration<ReferenceImage>
{
    public void Configure(EntityTypeBuilder<ReferenceImage> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.OriginalFileName).IsRequired().HasMaxLength(300);
        builder.Property(e => e.R2Key).IsRequired().HasMaxLength(300);
        builder.Property(e => e.ContentType).IsRequired().HasMaxLength(50);
        builder.Property(e => e.Sha256).HasMaxLength(64);

        builder.HasIndex(e => e.CreatedAt);

        builder.ToTable("reference_images");
    }
}
