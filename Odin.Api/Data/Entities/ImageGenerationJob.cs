using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Odin.Api.Data.Enums;

namespace Odin.Api.Data.Entities;

/// <summary>
/// Header row for a single OpenAI image-generation request (one per generate/edit call, sync or async).
/// Records the effective parameters, status, OpenAI token usage, and any error; the produced images are
/// child <see cref="GeneratedImage"/> rows. The <see cref="Id"/> doubles as the public job id and the
/// SignalR correlation id. <c>CreatedBy</c> (BaseEntity) is the requesting admin's Auth0 sub.
/// </summary>
public class ImageGenerationJob : BaseEntity
{
    public Guid Id { get; set; }

    public ImageGenerationMode Mode { get; set; } = ImageGenerationMode.Generation;
    public ImageGenerationStatus Status { get; set; } = ImageGenerationStatus.Pending;

    /// <summary>True when the request opted into background (Hangfire) processing.</summary>
    public bool IsAsync { get; set; }

    public string Prompt { get; set; } = string.Empty;

    /// <summary>The prompt gpt-image-2 actually used, when it revises the input (informational).</summary>
    public string? RevisedPrompt { get; set; }

    // Effective parameters (request overrides merged over the persisted defaults).
    public string Model { get; set; } = string.Empty;
    public string Size { get; set; } = string.Empty;
    public string Quality { get; set; } = string.Empty;
    public string Background { get; set; } = string.Empty;
    public string OutputFormat { get; set; } = string.Empty;
    public int? OutputCompression { get; set; }
    public string Moderation { get; set; } = string.Empty;
    public int N { get; set; }

    /// <summary>For edit jobs: the <see cref="ReferenceImage"/> ids fed to OpenAI (Postgres int[]).</summary>
    public int[]? ReferenceImageIds { get; set; }

    /// <summary>For edit jobs: optional mask reference image (the transparent area marks the region to regenerate).</summary>
    public int? MaskReferenceImageId { get; set; }

    /// <summary>For edit jobs: optional input fidelity (<c>high</c>/<c>low</c>). gpt-image-2 ignores it (always
    /// high fidelity) and only sends it when explicitly provided; kept for forward/back-compat.</summary>
    public string? InputFidelity { get; set; }

    // OpenAI token usage (persisted per job — first-party cost tracking that doesn't depend on the
    // lagging org-wide Administration API).
    public long? UsageInputTokens { get; set; }
    public long? UsageOutputTokens { get; set; }
    public long? UsageTotalTokens { get; set; }

    public string? ErrorCode { get; set; }
    public string? ErrorMessage { get; set; }

    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }

    public List<GeneratedImage> Images { get; set; } = [];
}

public class ImageGenerationJobConfiguration : IEntityTypeConfiguration<ImageGenerationJob>
{
    public void Configure(EntityTypeBuilder<ImageGenerationJob> builder)
    {
        builder.HasKey(e => e.Id);
        // Client-assigned Guid (set in the service), not DB-generated — the id is needed before the
        // OpenAI call to build R2 keys and the SignalR correlation.
        builder.Property(e => e.Id).ValueGeneratedNever();

        builder.Property(e => e.Mode).IsRequired().HasConversion<string>().HasMaxLength(20);
        builder.Property(e => e.Status).IsRequired().HasConversion<string>().HasMaxLength(20);
        builder.Property(e => e.Prompt).IsRequired();
        builder.Property(e => e.Model).IsRequired().HasMaxLength(50);
        builder.Property(e => e.Size).HasMaxLength(20);
        builder.Property(e => e.Quality).HasMaxLength(20);
        builder.Property(e => e.Background).HasMaxLength(20);
        builder.Property(e => e.OutputFormat).HasMaxLength(20);
        builder.Property(e => e.Moderation).HasMaxLength(20);
        builder.Property(e => e.InputFidelity).HasMaxLength(20);
        builder.Property(e => e.ErrorCode).HasMaxLength(100);
        builder.Property(e => e.ErrorMessage).HasMaxLength(2000);

        builder.HasMany(e => e.Images)
            .WithOne(i => i.Job)
            .HasForeignKey(i => i.JobId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(e => e.CreatedAt);
        builder.HasIndex(e => e.Status);

        builder.ToTable("image_generation_jobs");
    }
}
