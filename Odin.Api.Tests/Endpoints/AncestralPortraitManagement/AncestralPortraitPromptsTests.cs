using Odin.Api.Data.Enums;
using Odin.Api.Endpoints.AncestralPortraitManagement;
using Xunit;

namespace Odin.Api.Tests.Endpoints.AncestralPortraitManagement;

/// <summary>Guards the "you as your ancestor" prompt builder: identity-preserving lead, curated-vs-fallback creative,
/// the photoreal style, and that the user's own face is the subject (the reference), not an invented face.</summary>
public class AncestralPortraitPromptsTests
{
    [Fact]
    public void Build_LeadsWithIdentityPreservingInstruction()
    {
        var prompt = AncestralPortraitPrompts.Build("Celtic", "Iron Age peoples.", "Classical Antiquity", null);
        Assert.StartsWith("Create a photorealistic portrait of the SAME person", prompt);
        Assert.Contains("reference photographs", prompt);
        Assert.Contains("Celtic", prompt);
    }

    [Fact]
    public void Build_UsesCuratedPromptWhenPresent()
    {
        var curated = "A single Celtic warrior-farmer of the La Tène culture, torc at the neck, by a roundhouse.";
        var prompt = AncestralPortraitPrompts.Build("Celtic (600-50 BC)", "fallback desc", "Classical Antiquity", curated);
        Assert.Contains(curated, prompt);
        // Curated wins → the raw description is NOT appended.
        Assert.DoesNotContain("fallback desc", prompt);
    }

    [Fact]
    public void Build_FallsBackToNameAndDescriptionWithEra()
    {
        var prompt = AncestralPortraitPrompts.Build(
            "Western Steppe Herder (5000-2800 BC)", "Pastoralists of the Pontic-Caspian steppe.", "Bronze Age", null);
        // Short name (date-range qualifier dropped) + era grounding + the description.
        Assert.Contains("Western Steppe Herder", prompt);
        Assert.DoesNotContain("(5000-2800 BC)", prompt);
        Assert.Contains("of the Bronze Age", prompt);
        Assert.Contains("Pastoralists of the Pontic-Caspian steppe.", prompt);
    }

    [Fact]
    public void Build_AppliesPhotorealStyleAndForbidsModernWeaponsHelmets()
    {
        var prompt = AncestralPortraitPrompts.Build("Germanic", null, "Classical Antiquity", null);
        Assert.Contains("REALISTIC", prompt);
        Assert.Contains("NO helmet", prompt);
        Assert.Contains("NO weapons", prompt);
        Assert.Contains("NO watermark", prompt);
    }

    [Fact]
    public void Build_CapsLongDescriptions()
    {
        var huge = new string('x', 5000);
        // Even with the gender + hair clauses added, the prompt stays bounded.
        var prompt = AncestralPortraitPrompts.Build("Pop", huge, "Era", null, Gender.Female);
        // The description contribution is capped (≤ ~800 chars) so the prompt stays bounded.
        Assert.True(prompt.Length < 2200, $"prompt length was {prompt.Length}");
    }

    [Fact]
    public void Build_AppliesGenderConsistentPresentation()
    {
        var female = AncestralPortraitPrompts.Build("Celtic", null, "Iron Age", null, Gender.Female);
        Assert.Contains("WOMAN", female);
        Assert.Contains("feminine", female);
        Assert.Contains("WOMEN'S period-accurate attire", female);

        var male = AncestralPortraitPrompts.Build("Celtic", null, "Iron Age", null, Gender.Male);
        Assert.Contains("MAN", male);
        Assert.Contains("masculine", male);
        Assert.Contains("MEN'S period-accurate attire", male);

        // No gender supplied → no gendered clause (back-compat with callers that omit it).
        var none = AncestralPortraitPrompts.Build("Celtic", null, "Iron Age", null);
        Assert.DoesNotContain("distinctly feminine", none);
        Assert.DoesNotContain("distinctly masculine", none);
    }

    [Fact]
    public void Build_UsesBuiltInGenderedScene_ForSeededPopulation_WhenNoOverride()
    {
        // A seeded population (exact name) with no admin ImagePrompt override uses the built-in curated scene —
        // the women's variant for a female client, the men's variant otherwise — NOT the bare description.
        const string popName = "Illyrian (1200 - 250 BC)";

        var female = AncestralPortraitPrompts.Build(popName, "SENTINEL_DESCRIPTION", "Classical Antiquity", null, Gender.Female);
        Assert.Contains("A woman of", female);
        Assert.Contains("Illyrian", female);
        Assert.DoesNotContain("SENTINEL_DESCRIPTION", female); // curated scene wins over the description fallback
        Assert.Contains("fibulae", female);                    // period-researched women's attire

        var male = AncestralPortraitPrompts.Build(popName, "SENTINEL_DESCRIPTION", "Classical Antiquity", null, Gender.Male);
        Assert.Contains("A man of", male);
        Assert.DoesNotContain("SENTINEL_DESCRIPTION", male);
        Assert.Contains("pileus", male);                       // men's Illyrian dress

        // The two genders yield genuinely different creative, and both stay under the prompt rail.
        Assert.NotEqual(male, female);
        Assert.True(female.Length < 2400, $"female prompt length was {female.Length}");
        Assert.True(male.Length < 2400, $"male prompt length was {male.Length}");
    }

    [Fact]
    public void Build_PreservesSkinAndEyeColour_ButReplacesHairAndBeard_ForMaleViaSelfContainedDirective()
    {
        // Skin pigment + eye colour are PRESERVED for everyone. For MEN the hair/beard directive is SELF-CONTAINED in
        // the population's own descriptor (it owns its own "replace the photo's hair/beard" instruction) — there is no
        // shared/global male enforcement clause. Use a seeded population so the concrete descriptor is exercised.
        const string popName = "Celtic (600 - 50 BC)";

        var male = AncestralPortraitPrompts.Build(popName, null, "Classical Antiquity", null, Gender.Male);
        Assert.Contains("natural skin tone and eye colour", male);
        Assert.Contains("never lighten, darken or recolour the skin or eyes", male);
        Assert.Contains("lime-washed", male);                                                  // concrete Celtic descriptor preserved
        Assert.Contains("replacing the hair and facial hair from the reference photo", male);  // the population's OWN replace directive
        Assert.Contains("never keep the photo's own", male);
        Assert.True(male.Length < 2400, $"male prompt length was {male.Length}");
        // No shared male enforcement clause + no shared "His period hair and beard:" label any more.
        Assert.DoesNotContain("His period hair and beard:", male);
        Assert.DoesNotContain("MUST visibly change", male);

        var female = AncestralPortraitPrompts.Build(popName, null, "Classical Antiquity", null, Gender.Female);
        Assert.Contains("never lighten, darken or recolour the skin or eyes", female);
        Assert.Contains("Her period hairstyle:", female);                     // women keep the small shared clause
        Assert.Contains("Her hair MUST visibly change", female);
        Assert.Contains("clean-faced with no beard", female);
        Assert.DoesNotContain("replacing the hair and facial hair from the reference photo", female); // male-only directive
    }

    [Fact]
    public void Build_KeepsClientsOwnFacialHair_ForAnatolianNeolithicMale()
    {
        // The one population whose MALE portrait keeps the client's OWN beard from the uploaded photo (only the hair
        // restyles) — its descriptor carries a unique KEEP directive instead of the usual replace one.
        const string popName = "Anatolian Neolithic Farmer (8500 - 6000 BC)";
        var male = AncestralPortraitPrompts.Build(popName, null, "Hunter Gatherer and Neolithic Farmer", null, Gender.Male);

        Assert.Contains("shoulder-length dark wavy", male);                          // hair is still described/restyled
        Assert.Contains("keep his own facial hair from the reference photo", male);  // beard FOLLOWS the photo
        Assert.DoesNotContain("replacing the hair and facial hair", male);           // NOT the replace directive
        Assert.DoesNotContain("MUST visibly change", male);                          // no shared enforcement clause
    }

    [Fact]
    public void Build_StaysUnderLengthRail_ForLongestSeededScene()
    {
        // Longest population short-name + a near-max curated scene + female gender + hair clause is the worst case.
        var prompt = AncestralPortraitPrompts.Build(
            "Anatolian Neolithic Farmer (8500 - 6000 BC)", null, "Hunter Gatherer and Neolithic Farmer", null, Gender.Female);
        Assert.True(prompt.Length < 2400, $"prompt length was {prompt.Length}");
    }

    [Fact]
    public void Build_AdminOverrideBeatsBuiltInScene()
    {
        const string popName = "Illyrian (1200 - 250 BC)";
        const string curated = "A custom admin-authored Illyrian scene.";
        var prompt = AncestralPortraitPrompts.Build(popName, null, "Classical Antiquity", curated, Gender.Female);
        Assert.Contains(curated, prompt);
        Assert.DoesNotContain("A woman of", prompt); // the built-in scene is not used when an override is present
    }
}
