namespace Odin.Api.Endpoints.Admin.Models
{
    public class RecomputeG25DistancesContract
    {
        public class Request
        {
            public List<int>? InspectionIds { get; set; }
        }

        public class Response
        {
            public string Version { get; set; } = string.Empty;
            public int ErasConsidered { get; set; }
            public int InspectionsRequested { get; set; }
            public int InspectionsProcessed { get; set; }
            public int InspectionsSkipped { get; set; }
            public int ResultsUpserted { get; set; }
            public long DurationMs { get; set; }
        }
    }

    public class AdminG25InspectionContract
    {
        public class ListItem
        {
            public int Id { get; set; }
            public int OrderId { get; set; }
            public string FirstName { get; set; } = string.Empty;
            public string MiddleName { get; set; } = string.Empty;
            public string LastName { get; set; } = string.Empty;
            public string? UserEmail { get; set; }
            public bool HasCoordinates { get; set; }
            public int ResultCount { get; set; }
            public string? LatestResultsVersion { get; set; }
            public DateTime? LatestResultsUpdatedAt { get; set; }
        }
    }
}
