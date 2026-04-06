using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Odin.Api.Data.Entities;

public class ChangelogVersion : BaseEntity
{
    public int Id { get; set; }
    public string Version { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public DateTime ReleasedAt { get; set; }
    public bool IsPublished { get; set; }
    public List<ChangelogEntry> Entries { get; set; } = [];
}

public class ChangelogEntry : BaseEntity
{
    public int Id { get; set; }
    public int ChangelogVersionId { get; set; }
    public string Type { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int DisplayOrder { get; set; }
    public ChangelogVersion Version { get; set; } = null!;
}

public class ChangelogVersionConfiguration : IEntityTypeConfiguration<ChangelogVersion>
{
    public void Configure(EntityTypeBuilder<ChangelogVersion> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Version).IsRequired().HasMaxLength(20);
        builder.Property(e => e.Title).IsRequired().HasMaxLength(200);
        builder.Property(e => e.ReleasedAt).IsRequired();
        builder.Property(e => e.IsPublished).IsRequired().HasDefaultValue(false);

        builder.HasMany(e => e.Entries)
            .WithOne(e => e.Version)
            .HasForeignKey(e => e.ChangelogVersionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.ToTable("changelog_versions");
    }
}

public class ChangelogEntryConfiguration : IEntityTypeConfiguration<ChangelogEntry>
{
    public void Configure(EntityTypeBuilder<ChangelogEntry> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Type).IsRequired().HasMaxLength(20);
        builder.Property(e => e.Description).IsRequired().HasMaxLength(500);
        builder.Property(e => e.DisplayOrder).IsRequired();

        builder.ToTable("changelog_entries");
    }
}
