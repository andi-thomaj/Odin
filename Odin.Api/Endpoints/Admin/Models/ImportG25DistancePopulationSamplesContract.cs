namespace Odin.Api.Endpoints.Admin.Models
{
    public class ImportG25DistancePopulationSamplesContract
    {
        public class Response
        {
            public int TotalInFile { get; set; }
            public int Inserted { get; set; }
            public int SkippedExistingLabel { get; set; }
            public int SkippedInvalidEra { get; set; }
            public int SkippedMalformed { get; set; }
            public long DurationMs { get; set; }
        }
    }
}
