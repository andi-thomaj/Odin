using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Odin.Api.Data.Entities;

/// <summary>
/// A single image produced by an <see cref="ImageGenerationJob"/> (a job with <c>n &gt; 1</c> has several).
/// The bytes live in Cloudflare R2 under <see cref="R2Key"/>; only metadata is stored here. The public
/// URL is derived from the key via <c>IR2Storage.GetPublicUrl</c> — never persisted, so a CDN domain
/// change needs no data migration.
/// </summary>
public class GeneratedImage : BaseEntity
{
    public int Id { get; set; }

    public Guid JobId { get; set; }
    public ImageGenerationJob Job { get; set; } = null!;

    /// <summary>0-based position within the job's batch.</summary>
    public int BatchIndex { get; set; }

    public string R2Key { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
}

public class GeneratedImageConfiguration : IEntityTypeConfiguration<GeneratedImage>
{
    public void Configure(EntityTypeBuilder<GeneratedImage> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.R2Key).IsRequired().HasMaxLength(300);
        builder.Property(e => e.ContentType).IsRequired().HasMaxLength(50);

        builder.HasIndex(e => e.JobId);
        builder.HasIndex(e => e.CreatedAt);

        builder.ToTable("generated_images");
    }
}
