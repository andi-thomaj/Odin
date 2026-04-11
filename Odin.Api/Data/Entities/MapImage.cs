using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Odin.Api.Data.Entities;

public class MapImage : BaseEntity
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public int Width { get; set; }
    public int Height { get; set; }
}

public class MapImageConfiguration : IEntityTypeConfiguration<MapImage>
{
    public void Configure(EntityTypeBuilder<MapImage> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Name).IsRequired().HasMaxLength(100);
        builder.Property(e => e.FileName).IsRequired().HasMaxLength(200);
        builder.HasIndex(e => e.FileName).IsUnique();
        builder.ToTable("map_images");
    }
}
