using Odin.Api.Data.Enums;

namespace Odin.Api.Endpoints.HaplogroupHeatmap.Models
{
    /// <summary>Contracts for the admin "Import Y-haplogroup data" action and its last-run status.</summary>
    public static class HaplogroupImportContract
    {
        public sealed class StartResponse
        {
            public bool Enqueued { get; set; }
            public string? JobId { get; set; }
        }

        public sealed class StatusResponse
        {
            /// <summary>The most recent import run, or null if the import has never run.</summary>
            public RunDto? Latest { get; set; }

            /// <summary>True while a run is in progress (so the UI can disable the button / poll).</summary>
            public bool IsRunning { get; set; }
        }

        public sealed class RunDto
        {
            public int Id { get; set; }
            public DateTime StartedAt { get; set; }
            public DateTime? CompletedAt { get; set; }
            public HaplogroupImportStatus Status { get; set; }
            public string? DatasetVersion { get; set; }
            public int SampleCount { get; set; }
            public int NodeCount { get; set; }
            public int UnresolvedCount { get; set; }
            public string? Error { get; set; }
            public string TriggeredBy { get; set; } = string.Empty;
        }
    }
}
