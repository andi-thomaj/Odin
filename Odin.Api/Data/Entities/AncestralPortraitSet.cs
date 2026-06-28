using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Odin.Api.Data.Entities;

/// <summary>Lifecycle of one ancestral-portrait generation run.</summary>
public enum AncestralPortraitStatus
{
    Pending = 0,   // purchased, generation not started (e.g. waiting on a face-photo set)
    Running = 1,
    Succeeded = 2,
    Failed = 3,
}

/// <summary>
/// One paid "Through the Ages" generation for a qpAdm order: the add-on purchase (anti-replay via the unique
/// <see cref="TransactionId"/>) + its generated per-era <see cref="AncestralPortrait"/>s. The bytes live PRIVATE in R2
/// (served only via the authenticated download route — these are images of the user's face). One set per order.
/// </summary>
public class AncestralPortraitSet : BaseEntity
{
    public Guid Id { get; set; }

    public int OrderId { get; set; }

    public int UserId { get; set; }
    public User User { get; set; } = null!;

    /// <summary>Apple StoreKit transaction id that unlocked this set (unique → idempotent replay / anti double-unlock).</summary>
    public string TransactionId { get; set; } = string.Empty;

    public AncestralPortraitStatus Status { get; set; } = AncestralPortraitStatus.Pending;
    public string? Error { get; set; }

    public List<AncestralPortrait> Portraits { get; set; } = [];
}

public class AncestralPortraitSetConfiguration : IEntityTypeConfiguration<AncestralPortraitSet>
{
    public void Configure(EntityTypeBuilder<AncestralPortraitSet> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.TransactionId).IsRequired().HasMaxLength(100);
        builder.Property(e => e.Error).HasMaxLength(2000);
        builder.Property(e => e.Status).HasConversion<int>();

        builder.HasOne(e => e.User).WithMany().HasForeignKey(e => e.UserId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(e => e.Portraits).WithOne(p => p.Set).HasForeignKey(p => p.SetId).OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(e => e.TransactionId).IsUnique();   // anti-replay
        builder.HasIndex(e => e.OrderId).IsUnique();          // one set per order
        builder.HasIndex(e => e.UserId);

        builder.ToTable("ancestral_portrait_sets");
    }
}
