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
        var prompt = AncestralPortraitPrompts.Build("Pop", huge, "Era", null);
        // The description contribution is capped (≤ ~900 chars) so the prompt stays bounded.
        Assert.True(prompt.Length < 2200, $"prompt length was {prompt.Length}");
    }
}
