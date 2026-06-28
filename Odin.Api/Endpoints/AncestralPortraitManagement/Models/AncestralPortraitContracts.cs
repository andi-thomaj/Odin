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

/// <summary>The per-era group: the era + its top population + the generated variations.</summary>
public static class AncestralPortraitEraContract
{
    public class Response
    {
        public int EraId { get; set; }
        public string EraName { get; set; } = string.Empty;
        public string PopulationName { get; set; } = string.Empty;
        public List<AncestralPortraitContract.Response> Portraits { get; set; } = [];
    }
}

/// <summary>The whole set for an order: purchase/generation status + the per-era portrait groups.</summary>
public static class AncestralPortraitSetContract
{
    public class Response
    {
        public Guid? SetId { get; set; }
        public int OrderId { get; set; }
        /// <summary>"NotPurchased" | "Pending" | "Running" | "Succeeded" | "Failed".</summary>
        public string Status { get; set; } = "NotPurchased";
        public string? Error { get; set; }
        public List<AncestralPortraitEraContract.Response> Eras { get; set; } = [];
    }
}
