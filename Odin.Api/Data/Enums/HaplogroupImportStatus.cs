namespace Odin.Api.Data.Enums
{
    /// <summary>
    /// State of a Y-haplogroup heatmap import run (AADR + YFull → reference tables). Persisted on
    /// <see cref="Entities.HaplogroupImportRun"/> so the admin UI can show the last run's outcome and
    /// the distribution endpoint can key its cache off the latest <see cref="Completed"/> run.
    /// </summary>
    public enum HaplogroupImportStatus
    {
        /// <summary>The import is fetching from the tools API and loading the database.</summary>
        Running,

        /// <summary>The import finished and the reference tables now reflect this dataset version.</summary>
        Completed,

        /// <summary>The import failed (tools API unreachable, source unprovisioned, DB error). Reference tables are unchanged.</summary>
        Failed
    }
}
