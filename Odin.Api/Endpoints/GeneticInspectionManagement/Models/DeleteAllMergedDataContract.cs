namespace Odin.Api.Endpoints.GeneticInspectionManagement.Models
{
    /// <summary>
    /// Bulk delete of every still-<c>Ready</c> AADR merge bundle on the tools-api volume — the
    /// "Delete all merged data" action on the Input results grid (admin-only). Reports how many
    /// bundles were freed so the UI can confirm the reclaim.
    /// </summary>
    public class DeleteAllMergedDataContract
    {
        public class Response
        {
            /// <summary>How many merge bundles were deleted from the tools-api volume.</summary>
            public int DeletedCount { get; set; }
        }
    }
}
