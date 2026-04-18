using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Odin.Api.Data.Enums;

namespace Odin.Api.Data.Entities;

public class PromoCode
{
    public int Id { get; set; }
    /// <summary>Normalized (e.g. uppercase) unique code.</summary>
    public required string Code { get; set; }
    public PromoDiscountType DiscountType { get; set; }
    /// <summary>Percent (0–100) or fixed EUR depending on <see cref="DiscountType"/>.</summary>
    public decimal Value { get; set; }
    public DateTime? ValidFromUtc { get; set; }
    public DateTime? ValidUntilUtc { get; set; }
    public int? MaxRedemptions { get; set; }
    public int RedemptionCount { get; set; }
    public bool IsActive { get; set; } = true;
    /// <summary>When set, code applies only to this product line.</summary>
    public ServiceType? ApplicableService { get; set; }
    public List<QpadmOrder> Orders { get; set; } = [];
}

public class PromoCodeConfiguration : IEntityTypeConfiguration<PromoCode>
{
    public void Configure(EntityTypeBuilder<PromoCode> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Code).IsRequired().HasMaxLength(64);
        builder.Property(e => e.DiscountType).IsRequired().HasConversion<string>().HasMaxLength(32);
        builder.Property(e => e.Value).IsRequired().HasPrecision(18, 2);
        builder.Property(e => e.RedemptionCount).IsRequired().HasDefaultValue(0);
        builder.Property(e => e.IsActive).IsRequired().HasDefaultValue(true);
        builder.Property(e => e.ApplicableService).HasConversion<string?>().HasMaxLength(32);

        builder.HasIndex(e => e.Code).IsUnique();

        builder.ToTable("promo_codes");
    }
}
