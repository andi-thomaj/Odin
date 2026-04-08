using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Odin.Api.Data.Entities;

public class PaddlePayment
{
    public int Id { get; set; }
    public required string PaddleTransactionId { get; set; }
    public required string UserId { get; set; }
    public required string Status { get; set; }
    public decimal TotalAmount { get; set; }
    public required string Currency { get; set; }
    public string? ReceiptUrl { get; set; }
    public int? OrderId { get; set; }
    public Order? Order { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class PaddlePaymentConfiguration : IEntityTypeConfiguration<PaddlePayment>
{
    public void Configure(EntityTypeBuilder<PaddlePayment> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.PaddleTransactionId).IsRequired().HasMaxLength(64);
        builder.HasIndex(e => e.PaddleTransactionId).IsUnique();
        builder.Property(e => e.UserId).IsRequired().HasMaxLength(256);
        builder.HasIndex(e => e.UserId);
        builder.Property(e => e.Status).IsRequired().HasMaxLength(32);
        builder.Property(e => e.TotalAmount).IsRequired().HasPrecision(18, 2);
        builder.Property(e => e.Currency).IsRequired().HasMaxLength(8);
        builder.Property(e => e.ReceiptUrl).HasMaxLength(1024);

        builder.HasOne(e => e.Order)
            .WithMany()
            .HasForeignKey(e => e.OrderId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.ToTable("paddle_payments");
    }
}
