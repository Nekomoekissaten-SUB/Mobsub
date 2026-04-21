using FluentAssertions;
using Mobsub.SubtitleParse.AssTypes;
using Mobsub.SubtitleParse.Language;

namespace Mobsub.Test;

[TestClass]
public class AssProfileStrictnessAndCompletionTests
{
    [TestMethod]
    public void LibAss_Profile_WarnsWhenBlurExceedsMax()
    {
        var libass = new AssTextOptions(RendererProfile: AssRendererProfile.LibAss_0_17_4);
        var analysis = AssOverrideTextAnalyzer.Analyze("{\\blur150}x", context: null, options: libass);
        analysis.Diagnostics.Should().Contain(d => d.Code == "ass.override.profileRange");

        var vsfilter = new AssTextOptions(RendererProfile: AssRendererProfile.VsFilter);
        var analysisVs = AssOverrideTextAnalyzer.Analyze("{\\blur150}x", context: null, options: vsfilter);
        analysisVs.Diagnostics.Should().NotContain(d => d.Code == "ass.override.profileRange");
    }

    [TestMethod]
    public void LibAss_Profile_WarnsWhenBeExceedsMax()
    {
        var libass = new AssTextOptions(RendererProfile: AssRendererProfile.LibAss_0_17_4);
        var analysis = AssOverrideTextAnalyzer.Analyze("{\\be200}x", context: null, options: libass);
        analysis.Diagnostics.Should().Contain(d => d.Code == "ass.override.profileRange");

        var vsfilter = new AssTextOptions(RendererProfile: AssRendererProfile.VsFilter);
        var analysisVs = AssOverrideTextAnalyzer.Analyze("{\\be200}x", context: null, options: vsfilter);
        analysisVs.Diagnostics.Should().NotContain(d => d.Code == "ass.override.profileRange");
    }

    [TestMethod]
    public void Strictness_Strict_UpgradesFscNonStandardPayloadToWarning()
    {
        var strict = new AssTextOptions(Strictness: AssValidationStrictness.Strict);
        var analysis = AssOverrideTextAnalyzer.Analyze("{\\fsc50}x", context: null, options: strict);
        analysis.Diagnostics.Should().Contain(d => d.Code == "ass.override.nonStandardPayload" && d.Severity == AssSeverity.Warning);
    }

    [TestMethod]
    public void Completion_DefaultDialect_DoesNotSuggestVsFilterModTags()
    {
        var text = "{\\bl}";
        var analysis = AssOverrideTextAnalyzer.Analyze(text);
        int col = "{\\bl".Length;

        var completions = AssOverrideTextCompletionProvider.GetCompletions(text, new AssPosition(0, col), analysis);
        completions.Items.Should().Contain(i => i.InsertText.Equals("blur", StringComparison.OrdinalIgnoreCase));
        completions.Items.Should().NotContain(i => i.InsertText.Equals("blend", StringComparison.OrdinalIgnoreCase));

        var mod = new AssTextOptions(Dialect: AssTextDialect.VsFilterMod);
        var completionsMod = AssOverrideTextCompletionProvider.GetCompletions(text, new AssPosition(0, col), analysis, mod);
        completionsMod.Items.Should().Contain(i => i.InsertText.Equals("blend", StringComparison.OrdinalIgnoreCase));
    }
}

