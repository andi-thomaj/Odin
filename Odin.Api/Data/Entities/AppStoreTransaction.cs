using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Odin.Api.Data.Enums;

namespace Odin.Api.Data.Entities
{
    /// <summary>
    /// A validated Apple StoreKit 2 in-app purchase, recorded server-side so a paid qpAdm/G25 order is
    /// created exactly once per Apple transaction. The unique <c>(App, TransactionId)</c> index is the
    /// anti-replay / idempotency guard: re-submitting the same signed transaction (e.g. the iOS app
    /// replaying an unfinished transaction after a dropped response) returns the order it already created
    /// rather than creating a second one. iOS-only today — the web has no IAP.
    /// </summary>
    public class AppStoreTransaction : BaseEntity
    {
        public int Id { get; set; }

        /// <summary>Apple's <c>transactionId</c> — unique per purchase; the idempotency / anti-replay key.</summary>
        public string TransactionId { get; set; } = string.Empty;

        /// <summary>Apple's <c>originalTransactionId</c> (equals <see cref="TransactionId"/> for a fresh consumable buy).</summary>
        public string OriginalTransactionId { get; set; } = string.Empty;

        /// <summary>App Store product id, e.g. <c>io.ancestrify.app.qpadm</c>.</summary>
        public string ProductId { get; set; } = string.Empty;

        /// <summary>Service the product maps to (validated to equal the order's requested service).</summary>
        public ServiceType Service { get; set; }

        public AppStoreTransactionStatus Status { get; set; } = AppStoreTransactionStatus.Verified;

        /// <summary>The qpAdm order this transaction was consumed to create (set when <see cref="Service"/> is qpAdm).</summary>
        public int? QpadmOrderId { get; set; }

        /// <summary>The G25 order this transaction was consumed to create (set when <see cref="Service"/> is g25).</summary>
        public int? G25OrderId { get; set; }

        public DateTime PurchaseDate { get; set; }

        /// <summary>Apple environment the transaction was signed in: <c>Production</c> | <c>Sandbox</c> | <c>Xcode</c>.</summary>
        public string Environment { get; set; } = string.Empty;

        /// <summary>The raw signed transaction JWS, retained for audit / dispute resolution.</summary>
        public string RawJws { get; set; } = string.Empty;
    }

    public class AppStoreTransactionConfiguration : IEntityTypeConfiguration<AppStoreTransaction>
    {
        public void Configure(EntityTypeBuilder<AppStoreTransaction> builder)
        {
            builder.HasKey(e => e.Id);
            builder.Property(e => e.TransactionId).IsRequired().HasMaxLength(100);
            builder.Property(e => e.OriginalTransactionId).HasMaxLength(100);
            builder.Property(e => e.ProductId).IsRequired().HasMaxLength(200);
            builder.Property(e => e.Service).IsRequired().HasConversion<string>().HasMaxLength(20);
            builder.Property(e => e.Status).IsRequired().HasConversion<string>().HasMaxLength(20);
            builder.Property(e => e.Environment).HasMaxLength(20);
            builder.Property(e => e.RawJws).HasColumnType("text");

            // One paid order per Apple transaction. UNIQUE is the real race-proof guard behind CreatePaidAsync
            // and backs the idempotency lookup.
            builder.HasIndex(e => e.TransactionId).IsUnique();

            builder.ToTable("app_store_transactions");
        }
    }
}
