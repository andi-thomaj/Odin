namespace Odin.Api.Configuration;

/// <summary>
/// Operational limits applied to order submission and recompute. Bound from the
/// <c>OrderLimits</c> section of configuration. All values have sane defaults — set
/// keys in <c>appsettings.json</c> / env vars only to override (e.g. for promotions
/// or A/B tests that need a wider selection budget).
/// </summary>
public class OrderLimitsOptions
{
    public const string SectionName = "OrderLimits";

    /// <summary>Maximum number of distinct ethnicities (countries) a qpAdm submission can target.</summary>
    public int MaxEthnicities { get; set; } = 4;

    /// <summary>Maximum number of regions within a single ethnicity a qpAdm submission can target.</summary>
    public int MaxRegionsPerEthnicity { get; set; } = 4;

    /// <summary>How many population rows the G25 distance calculator returns per (inspection, era) pair.</summary>
    public int G25DistanceMaxResults { get; set; } = 25;
}
