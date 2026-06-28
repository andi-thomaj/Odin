using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Odin.Api.Data.Entities;

/// <summary>
/// One generated "ancestral self" portrait (the user reimagined as a population) — bytes PRIVATE in R2 under
/// <see cref="R2Key"/>, served only via the authenticated download route. Several variations per era share an
/// (<see cref="SetId"/>, <see cref="EraId"/>); the user marks one <see cref="IsSelected"/> per era.
/// </summary>
public class AncestralPortrait : BaseEntity
{
    public int Id { get; set; }

    public Guid SetId { get; set; }
    public AncestralPortraitSet Set { get; set; } = null!;

    public int EraId { get; set; }
    public string EraName { get; set; } = string.Empty;
    public string PopulationName { get; set; } = string.Empty;

    public string R2Key { get; set; } = string.Empty;
    public string ContentType { get; set; } = "image/jpeg";
    public long ByteSize { get; set; }

    /// <summary>0-based variation index within its (set, era).</summary>
    public int VariationIndex { get; set; }
    /// <summary>The user's chosen variation for this era (drives the share set + reel). Exactly one per era.</summary>
    public bool IsSelected { get; set; }
}

public class AncestralPortraitConfiguration : IEntityTypeConfiguration<AncestralPortrait>
{
    public void Configure(EntityTypeBuilder<AncestralPortrait> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.EraName).IsRequired().HasMaxLength(200);
        builder.Property(e => e.PopulationName).IsRequired().HasMaxLength(200);
        builder.Property(e => e.R2Key).IsRequired().HasMaxLength(300);
        builder.Property(e => e.ContentType).IsRequired().HasMaxLength(50);

        builder.HasIndex(e => e.SetId);
        builder.HasIndex(e => new { e.SetId, e.EraId });

        builder.ToTable("ancestral_portraits");
    }
}
