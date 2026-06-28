namespace Odin.Api.Configuration;

/// <summary>
/// Cost/abuse guardrails for the paid "Through the Ages" ancestral-portrait generation. One unlock generates at most
/// <see cref="MaxEras"/> × <see cref="VariationsPerEra"/> images at a fixed size/quality, so spend is bounded per
/// purchase. Tunable via the <c>AncestralPortraitLimits</c> config section.
/// </summary>
public sealed class AncestralPortraitLimitsOptions
{
    public const string SectionName = "AncestralPortraitLimits";

    /// <summary>Cap on eras turned into portraits (one portrait group per era).</summary>
    public int MaxEras { get; set; } = 6;

    /// <summary>Variations generated per era (the user picks one).</summary>
    public int VariationsPerEra { get; set; } = 2;

    /// <summary>Face reference photos passed to gpt-image-2 per generation (≤16; more = better identity, more cost).</summary>
    public int MaxFaceReferences { get; set; } = 6;

    /// <summary>Portrait dimensions (portrait 2:3 reads well in the share set + reel).</summary>
    public string Size { get; set; } = "1024x1536";

    /// <summary>gpt-image-2 quality tier (medium balances likeness vs. cost/latency).</summary>
    public string Quality { get; set; } = "medium";
}
