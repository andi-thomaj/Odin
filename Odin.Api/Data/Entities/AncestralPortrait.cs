using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Odin.Api.Data.Entities;

/// <summary>
/// One generated "ancestral self" portrait (the user reimagined as a population) — bytes PRIVATE in R2 under
/// <see cref="R2Key"/>, served only via the authenticated download route. The set generates a portrait group for
/// EVERY population in EVERY era, so a group is keyed by (<see cref="SetId"/>, <see cref="EraId"/>,
/// <see cref="PopulationId"/>); several <see cref="VariationIndex"/> variations may share that triple, and the user
/// marks any subset <see cref="IsSelected"/>.
/// </summary>
public class AncestralPortrait : BaseEntity
{
    public int Id { get; set; }

    public Guid SetId { get; set; }
    public AncestralPortraitSet Set { get; set; } = null!;

    public int EraId { get; set; }
    public string EraName { get; set; } = string.Empty;
    /// <summary>The qpAdm population this portrait reimagines the user as (one portrait group per population per era).</summary>
    public int PopulationId { get; set; }
    public string PopulationName { get; set; } = string.Empty;

    public string R2Key { get; set; } = string.Empty;
    public string ContentType { get; set; } = "image/jpeg";
    public long ByteSize { get; set; }

    /// <summary>0-based variation index within its (set, era, population).</summary>
    public int VariationIndex { get; set; }
    /// <summary>Whether the user kept this variation (drives the share set + reel). Multi-select across all groups.</summary>
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
        builder.HasIndex(e => new { e.SetId, e.EraId, e.PopulationId });

        builder.ToTable("ancestral_portraits");
    }
}
