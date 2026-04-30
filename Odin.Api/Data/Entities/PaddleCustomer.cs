using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Odin.Api.Data.Entities;

/// <summary>
/// Local mirror of a Paddle customer. <see cref="UserId"/> is the bridge from our Auth0
/// identity to Paddle's billing identity — populated when we resolve a customer via
/// <c>custom_data.user_id</c> on the first transaction or when we explicitly create a
/// customer for a user.
/// </summary>
public class PaddleCustomer
{
    public int Id { get; set; }

    /// <summary>Paddle customer id, prefixed <c>ctm_</c>.</summary>
    public required string PaddleCustomerId { get; set; }

    public required string Email { get; set; }
    public string? Name { get; set; }
    public string? Locale { get; set; }
    public string? MarketingConsent { get; set; }
    public required string Status { get; set; }
    public string? CustomData { get; set; }

    /// <summary>Auth0 subject (sub) of the linked application user, when known.</summary>
    public string? UserId { get; set; }

    public DateTimeOffset? PaddleCreatedAt { get; set; }
    public DateTimeOffset? PaddleUpdatedAt { get; set; }
    public DateTime LastSyncedAt { get; set; }
}

public class PaddleCustomerConfiguration : IEntityTypeConfiguration<PaddleCustomer>
{
    public void Configure(EntityTypeBuilder<PaddleCustomer> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.PaddleCustomerId).IsRequired().HasMaxLength(64);
        builder.HasIndex(e => e.PaddleCustomerId).IsUnique();

        builder.Property(e => e.Email).IsRequired().HasMaxLength(320);
        builder.HasIndex(e => e.Email);

        builder.Property(e => e.Name).HasMaxLength(200);
        builder.Property(e => e.Locale).HasMaxLength(16);
        builder.Property(e => e.MarketingConsent).HasMaxLength(16);
        builder.Property(e => e.Status).IsRequired().HasMaxLength(32);
        builder.Property(e => e.CustomData).HasColumnType("jsonb");

        builder.Property(e => e.UserId).HasMaxLength(256);
        builder.HasIndex(e => e.UserId);

        builder.Property(e => e.LastSyncedAt).IsRequired();

        builder.ToTable("paddle_customers");
    }
}
