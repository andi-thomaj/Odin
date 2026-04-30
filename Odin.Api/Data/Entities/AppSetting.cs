using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Odin.Api.Data.Entities;

/// <summary>
/// Generic key/value store for runtime-tunable feature flags and app-level toggles
/// (e.g. <c>AdminCanSkipPayment</c>). Reads are cached in memory by
/// <c>AppSettingsService</c>; writes go through the same service so the cache stays
/// consistent.
/// </summary>
public class AppSetting
{
    public int Id { get; set; }
    public required string Key { get; set; }
    public required string Value { get; set; }
    public string? Description { get; set; }
    public DateTime UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }
}

public class AppSettingConfiguration : IEntityTypeConfiguration<AppSetting>
{
    public void Configure(EntityTypeBuilder<AppSetting> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Key).IsRequired().HasMaxLength(100);
        builder.HasIndex(e => e.Key).IsUnique();

        builder.Property(e => e.Value).IsRequired().HasMaxLength(2000);
        builder.Property(e => e.Description).HasMaxLength(500);
        builder.Property(e => e.UpdatedAt).IsRequired();
        builder.Property(e => e.UpdatedBy).HasMaxLength(256);

        builder.ToTable("app_settings");
    }
}
