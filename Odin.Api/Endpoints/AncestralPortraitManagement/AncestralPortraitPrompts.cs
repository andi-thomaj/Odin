using System.Text;
using Odin.Api.Data.Enums;

namespace Odin.Api.Endpoints.AncestralPortraitManagement;

/// <summary>
/// Builds the <c>gpt-image-2</c> EDIT prompt for an "ancestral self" portrait: the user's own face photos are the
/// reference images, and the prompt reimagines that SAME person as a given ancestral population. Pure + deterministic
/// so it's unit-testable. The per-population creative is sourced from the admin-curated <c>QpadmPopulation.ImagePrompt</c>
/// when present, else built from the population's name + description; both are wrapped with an identity-preserving
/// instruction (so the output keeps the user's likeness) and the shared photoreal portrait style.
/// </summary>
public static class AncestralPortraitPrompts
{
    /// <summary>Identity-preserving lead — gpt-image-2 edits the reference (the user's face) rather than inventing a face.</summary>
    private const string IdentityLead =
        "Create a photorealistic portrait of the SAME person shown in the reference photographs — keep their exact " +
        "facial identity, bone structure, and likeness — reimagined as ";

    /// <summary>Shared rendering style (mirrors the population-avatar AVATAR_IMAGE_STYLE: photoreal, period-accurate,
    /// rich cultural backdrop, no weapons/HUD/text), adapted for a vertical 9:16-ish portrait of the user.</summary>
    private const string Style =
        " Render a REALISTIC, finely detailed human portrait — natural skin texture, hair, fabric and materials; soft " +
        "cinematic key light with a gentle rim; a deep, slightly muted period-mood palette — NOT cartoon, pixel art or " +
        "cel-shaded. A VERTICAL portrait-orientation, head-and-shoulders portrait cropped at roughly mid-chest, the " +
        "single subject centered and facing the camera. " +
        "Period-accurate upper-body dress, headwear and adornments for this people; NO modern objects, NO helmet, " +
        "NO weapons or hand-held objects. Behind the subject, a full-bleed scene of this culture's iconography — " +
        "architecture, dwellings, tools, textiles, ornaments and landscape. NO frame, NO border, NO UI/HUD, NO text, " +
        "NO watermark.";

    /// <summary>
    /// The full edit prompt for one population. <paramref name="curatedImagePrompt"/> is
    /// <c>QpadmPopulation.ImagePrompt</c> (may be null/blank); <paramref name="name"/> + <paramref name="description"/>
    /// are the fallback creative. <paramref name="eraName"/> grounds the period.
    /// </summary>
    public static string Build(string name, string? description, string? eraName, string? curatedImagePrompt,
        Gender? gender = null)
    {
        var subject = BuildSubject(name, description, eraName, curatedImagePrompt);
        return IdentityLead + subject + GenderClause(gender) + Style;
    }

    /// <summary>Enforces a gender-consistent presentation — the client's gender drives feminine vs masculine
    /// clothing/dress, hair and adornments (so the portrait matches the person, not just the population).</summary>
    private static string GenderClause(Gender? gender) => gender switch
    {
        Gender.Female =>
            " The subject is a WOMAN — give her a distinctly feminine presentation: the WOMEN'S period-accurate attire " +
            "of this culture (dress/garments, hairstyle, headwear and jewellery), feminine grooming and silhouette.",
        Gender.Male =>
            " The subject is a MAN — give him a distinctly masculine presentation: the MEN'S period-accurate attire " +
            "of this culture (garments, hairstyle, headwear and adornments), masculine grooming and silhouette.",
        _ => string.Empty,
    };

    private static string BuildSubject(string name, string? description, string? eraName, string? curatedImagePrompt)
    {
        var era = (eraName ?? string.Empty).Trim();
        var cleanName = ShortName(name);

        if (!string.IsNullOrWhiteSpace(curatedImagePrompt))
        {
            // The curated prompt already describes the population + scene; lead with the "you as" framing.
            return $"a {cleanName}. {curatedImagePrompt!.Trim()}";
        }

        var sb = new StringBuilder();
        sb.Append("a ").Append(cleanName);
        if (era.Length > 0) sb.Append(" of the ").Append(era);
        sb.Append('.');

        var desc = (description ?? string.Empty).Trim();
        if (desc.Length > 0)
        {
            // Cap the description so the prompt stays well within model limits.
            sb.Append(' ').Append(desc.Length > 900 ? desc[..900] : desc);
        }
        return sb.ToString();
    }

    /// <summary>Drops a trailing "(…)" date-range qualifier: "Western Steppe Herder (5000-2800 BC)" → "Western Steppe Herder".</summary>
    private static string ShortName(string name)
    {
        var trimmed = (name ?? string.Empty).Trim();
        var idx = trimmed.IndexOf(" (", StringComparison.Ordinal);
        return idx > 0 ? trimmed[..idx] : trimmed;
    }
}
