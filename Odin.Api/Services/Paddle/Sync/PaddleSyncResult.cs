namespace Odin.Api.Services.Paddle.Sync;

/// <summary>Aggregate result of a sync run for a single resource type.</summary>
public sealed class PaddleSyncResult
{
    public required string Resource { get; init; }
    public int Inserted { get; set; }
    public int Updated { get; set; }
    public int Skipped { get; set; }
    public int Failed { get; set; }

    public int Total => Inserted + Updated + Skipped + Failed;

    public List<string> Errors { get; } = [];
}
