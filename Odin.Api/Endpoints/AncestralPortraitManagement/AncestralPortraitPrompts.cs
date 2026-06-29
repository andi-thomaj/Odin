using System.Text;
using Odin.Api.Data.Enums;

namespace Odin.Api.Endpoints.AncestralPortraitManagement;

/// <summary>
/// Builds the <c>gpt-image-2</c> EDIT prompt for an "ancestral self" portrait: the user's own face photos are the
/// reference images, and the prompt reimagines that SAME person as a given ancestral population. Pure + deterministic
/// so it's unit-testable. The per-population creative is sourced, in priority order, from: (1) the admin-curated
/// <c>QpadmPopulation.ImagePrompt</c> override when present, else (2) a built-in GENDER-SPECIFIC scene for the seeded
/// population (<see cref="AncestralPortraitScenes"/> — so the portrait resembles the population's own photo, with a
/// women's variant for female clients and a men's variant otherwise), else (3) the population's name + description.
/// In every case the creative is wrapped with an identity-preserving instruction (keep the user's bone structure,
/// likeness, natural skin tone and eye colour — but restyle the hair, and for men the beard, to the ancestral
/// period), a gender clause, and the shared photoreal portrait style.
/// </summary>
public static class AncestralPortraitPrompts
{
    /// <summary>Identity-preserving lead — gpt-image-2 edits the reference (the user's face). Bone structure, likeness,
    /// natural SKIN TONE and EYE COLOUR are kept exactly; only the HAIR (and, for men, the BEARD) is restyled to the
    /// ancestral period — so the portrait reads as the user as their ancestor, not a recoloured/re-raced stranger.</summary>
    private const string IdentityLead =
        "Create a photorealistic portrait of the SAME person shown in the reference photographs — keep their exact " +
        "facial bone structure and likeness, and keep their natural skin tone and eye colour exactly as in the reference " +
        "(never lighten, darken or recolour the skin or eyes); their hair and any beard are restyled to the period — " +
        "reimagined as ";

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
        var subject = BuildSubject(name, description, eraName, curatedImagePrompt, gender);
        return IdentityLead + subject + GenderClause(gender) + Style;
    }

    /// <summary>Enforces a gender-consistent presentation — the client's gender drives feminine vs masculine clothing
    /// and adornments AND the hair/beard restyle: women get this culture's women's hairstyle and are clean-faced; men
    /// get this culture's men's hair AND beard. (Skin tone + eye colour are preserved by <see cref="IdentityLead"/>.)</summary>
    private static string GenderClause(Gender? gender) => gender switch
    {
        Gender.Female =>
            " The subject is a WOMAN — give her a distinctly feminine presentation: the WOMEN'S period-accurate attire " +
            "of this culture (dress, headwear and jewellery), with her HAIR restyled into this culture's women's hairstyle; " +
            "clean-faced, no beard.",
        Gender.Male =>
            " The subject is a MAN — give him a distinctly masculine presentation: the MEN'S period-accurate attire " +
            "of this culture (garments, headwear and adornments), with his HAIR and BEARD restyled into this culture's " +
            "men's style.",
        _ => string.Empty,
    };

    private static string BuildSubject(string name, string? description, string? eraName, string? curatedImagePrompt,
        Gender? gender)
    {
        var era = (eraName ?? string.Empty).Trim();
        var cleanName = ShortName(name);

        // 1. Admin per-population override wins (QpadmPopulation.ImagePrompt).
        if (!string.IsNullOrWhiteSpace(curatedImagePrompt))
        {
            // The curated prompt already describes the population + scene; lead with the "you as" framing.
            return $"a {cleanName}. {curatedImagePrompt!.Trim()}";
        }

        // 2. Built-in, gender-specific curated scene for a seeded population — so the portrait resembles the
        //    population's own photo (period dress + cultural background) instead of the bare name+description.
        //    Female clients get the period-researched women's variant; male/unknown get the men's variant.
        if (AncestralPortraitScenes.TryGetScene(name, gender, out var scene) && !string.IsNullOrWhiteSpace(scene))
        {
            return $"a {cleanName}. {scene.Trim()}";
        }

        // 3. Fallback: short name + era grounding + the population's encyclopedic description (capped).
        var sb = new StringBuilder();
        sb.Append("a ").Append(cleanName);
        if (era.Length > 0) sb.Append(" of the ").Append(era);
        sb.Append('.');

        var desc = (description ?? string.Empty).Trim();
        if (desc.Length > 0)
        {
            // Cap the description so the prompt stays well within model limits (and under the ~2200-char rail once
            // the identity lead, gender clause and style block are added).
            sb.Append(' ').Append(desc.Length > 800 ? desc[..800] : desc);
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
