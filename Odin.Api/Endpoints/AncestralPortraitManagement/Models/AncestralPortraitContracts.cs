namespace Odin.Api.Endpoints.AncestralPortraitManagement.Models;

/// <summary>iOS sends the StoreKit 2 signed transaction JWS to unlock the add-on for an order.</summary>
public static class PurchaseAncestralPortraitsContract
{
    public class Request
    {
        public string AppStoreTransaction { get; set; } = string.Empty;
    }
}

/// <summary>One generated portrait variation (bytes fetched from the authenticated <c>DownloadUrl</c>, never a public URL).</summary>
public static class AncestralPortraitContract
{
    public class Response
    {
        public int Id { get; set; }
        public int VariationIndex { get; set; }
        public bool IsSelected { get; set; }
        public string DownloadUrl { get; set; } = string.Empty;
    }
}

/// <summary>One portrait group: a (era, population) pair + its generated variations. A set has ONE group per
/// population per era (ranked by ancestry %), so several groups can share an <see cref="EraId"/> — the client keys
/// each group on (<see cref="EraId"/>, <see cref="PopulationId"/>). (The list field on the set is still named
/// <c>Eras</c> for back-compat; each entry is now an era×population group.)</summary>
public static class AncestralPortraitEraContract
{
    public class Response
    {
        public int EraId { get; set; }
        public string EraName { get; set; } = string.Empty;
        public int PopulationId { get; set; }
        public string PopulationName { get; set; } = string.Empty;
        public List<AncestralPortraitContract.Response> Portraits { get; set; } = [];
    }
}

/// <summary>The whole set for an order: purchase/generation status + the per-era portrait groups. **No cost/usage
/// here** — that's deliberately NOT exposed to the iOS app/user (only the AdminOnly usage endpoint surfaces it).</summary>
public static class AncestralPortraitSetContract
{
    public class Response
    {
        public Guid? SetId { get; set; }
        public int OrderId { get; set; }
        /// <summary>"NotPurchased" | "Pending" | "Running" | "Succeeded" | "Failed".</summary>
        public string Status { get; set; } = "NotPurchased";
        public string? Error { get; set; }
        /// <summary>When this iteration was purchased (for ordering/labelling the history). Null for an empty placeholder.</summary>
        public DateTime? CreatedAt { get; set; }
        public List<AncestralPortraitEraContract.Response> Eras { get; set; } = [];
    }
}

/// <summary>Admin aggregate of AI-portrait spend across all runs (first-party, real-time — independent of OpenAI's
/// lagging usage API). AdminOnly; consumed by the WEB admin app, never the iOS client.</summary>
public static class AncestralPortraitUsageContract
{
    public class Response
    {
        public int TotalRuns { get; set; }
        public int SucceededRuns { get; set; }
        public int TotalImages { get; set; }
        public long TotalInputTokens { get; set; }
        public long TotalOutputTokens { get; set; }
        public long TotalTokens { get; set; }
        public decimal TotalEstimatedCostUsd { get; set; }
    }
}
