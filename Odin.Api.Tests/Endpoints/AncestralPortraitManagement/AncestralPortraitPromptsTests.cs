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
        // Even with a gender clause added, the prompt stays bounded.
        var prompt = AncestralPortraitPrompts.Build("Pop", huge, "Era", null, Gender.Female);
        // The description contribution is capped (≤ ~900 chars) so the prompt stays bounded.
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

        // The two genders yield genuinely different creative, and both stay bounded.
        Assert.NotEqual(male, female);
        Assert.True(female.Length < 2200, $"female prompt length was {female.Length}");
        Assert.True(male.Length < 2200, $"male prompt length was {male.Length}");
    }

    [Fact]
    public void Build_PreservesSkinAndEyeColour_ButRestylesHairAndBeard()
    {
        // Skin pigment + eye colour are preserved for everyone; hair is restyled for everyone, beard only for men.
        var male = AncestralPortraitPrompts.Build("Celtic", null, "Iron Age", null, Gender.Male);
        Assert.Contains("skin tone", male);
        Assert.Contains("eye colour", male);
        Assert.Contains("never lighten, darken or recolour the skin or eyes", male);
        Assert.Contains("HAIR and BEARD restyled", male);

        var female = AncestralPortraitPrompts.Build("Celtic", null, "Iron Age", null, Gender.Female);
        Assert.Contains("never lighten, darken or recolour the skin or eyes", female);
        Assert.Contains("HAIR restyled", female);
        Assert.Contains("no beard", female);
        Assert.DoesNotContain("HAIR and BEARD restyled", female); // women aren't given a beard
    }

    [Fact]
    public void Build_StaysUnderLengthRail_ForLongestSeededScene()
    {
        // Longest population short-name + a near-max curated scene + female gender clause is the worst case.
        var prompt = AncestralPortraitPrompts.Build(
            "Anatolian Neolithic Farmer (8500 - 6000 BC)", null, "Hunter Gatherer and Neolithic Farmer", null, Gender.Female);
        Assert.True(prompt.Length < 2200, $"prompt length was {prompt.Length}");
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
