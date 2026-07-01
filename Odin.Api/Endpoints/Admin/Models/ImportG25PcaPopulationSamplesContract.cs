namespace Odin.Api.Endpoints.Admin.Models
{
    public class ImportG25PcaPopulationSamplesContract
    {
        public class Response
        {
            public int TotalInFile { get; set; }
            public int Inserted { get; set; }

            // PCA labels aren't unique (many individuals per population), so an existing row is keyed on
            // the (era, label, ids) triple — the same identity the seed generator dedups on.
            public int SkippedExisting { get; set; }
            public int SkippedInvalidEra { get; set; }
            public int SkippedMalformed { get; set; }
            public long DurationMs { get; set; }
        }
    }
}
