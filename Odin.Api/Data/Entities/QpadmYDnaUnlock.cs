using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Odin.Api.Data.Entities;

/// <summary>
/// Records that a user has paid the one-time ($9.99) unlock for an order's Y-DNA results. The Y-DNA clade is
/// withheld from the qpAdm-result response until a matching row exists (server-enforced paywall). A unit is keyed
/// by Apple <see cref="TransactionId"/> (unique → anti-replay / idempotent re-submit) and the <see cref="OrderId"/>
/// it unlocks (consumable, one purchase = one order — mirrors the per-order AI-portraits add-on).
/// </summary>
public class QpadmYDnaUnlock : BaseEntity
{
    public int Id { get; set; }

    /// <summary>The qpAdm order whose Y-DNA result this unlocks.</summary>
    public int OrderId { get; set; }

    /// <summary>The owning user (resolved from the Auth0 sub at purchase time).</summary>
    public int UserId { get; set; }

    /// <summary>The consumed Apple StoreKit transaction id (unique — replaying it returns the existing unlock).</summary>
    public string TransactionId { get; set; } = string.Empty;
}

public class QpadmYDnaUnlockConfiguration : IEntityTypeConfiguration<QpadmYDnaUnlock>
{
    public void Configure(EntityTypeBuilder<QpadmYDnaUnlock> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.TransactionId).IsRequired().HasMaxLength(100);
        builder.HasIndex(e => e.TransactionId).IsUnique();   // anti-replay
        builder.HasIndex(e => e.OrderId);                     // the unlock lookup on every result view

        builder.ToTable("qpadm_ydna_unlocks");
    }
}
