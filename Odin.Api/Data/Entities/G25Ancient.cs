using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Odin.Api.Data.Entities;

public class G25Ancient : BaseEntity
{
    public int Id { get; set; }
    public string Label { get; set; } = string.Empty;
    public string Coordinates { get; set; } = string.Empty;
}

public class G25AncientConfiguration : IEntityTypeConfiguration<G25Ancient>
{
    public void Configure(EntityTypeBuilder<G25Ancient> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Label).IsRequired().HasMaxLength(500);
        builder.Property(e => e.Coordinates).IsRequired().HasColumnType("text");
        builder.HasIndex(e => e.Label);

        builder.ToTable("g25_ancients");
    }
}
