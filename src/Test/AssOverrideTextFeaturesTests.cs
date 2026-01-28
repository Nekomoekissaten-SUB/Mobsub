using FluentAssertions;
using Mobsub.SubtitleParse.Language;

namespace Mobsub.Test;

[TestClass]
public class AssOverrideTextFeaturesTests
{
    [TestMethod]
    public void OverrideText_Analyze_ReportsUnknownTag()
    {
        var analysis = AssOverrideTextAnalyzer.Analyze("{\\zz1}Hi");
        analysis.Diagnostics.Should().Contain(d => d.Code == "ass.override.unknownTag");
    }

    [TestMethod]
    public void OverrideText_Analyze_ValidatesFunctionTags()
    {
        var ok = AssOverrideTextAnalyzer.Analyze("{\\pos(1,2)}Hi");
        ok.Diagnostics.Should().NotContain(d => d.Code == "ass.override.functionInvalid");

        var bad = AssOverrideTextAnalyzer.Analyze("{\\pos(1,)}Hi");
        bad.Diagnostics.Should().Contain(d => d.Code == "ass.override.functionInvalid");
    }

    [TestMethod]
    public void OverrideText_Analyze_Move_SupportsOverloads()
    {
        var ok4 = AssOverrideTextAnalyzer.Analyze("{\\move(1,2,3,4)}Hi");
        ok4.Diagnostics.Should().NotContain(d => d.Code == "ass.override.functionInvalid");

        var ok6 = AssOverrideTextAnalyzer.Analyze("{\\move(1,2,3,4,100,200)}Hi");
        ok6.Diagnostics.Should().NotContain(d => d.Code == "ass.override.functionInvalid");

        var bad5 = AssOverrideTextAnalyzer.Analyze("{\\move(1,2,3,4,100)}Hi");
        bad5.Diagnostics.Should().Contain(d => d.Code == "ass.override.functionInvalid");
    }

    [TestMethod]
    public void OverrideText_Analyze_WarnsWhenRelativeTimesExceedEventDuration()
    {
        var ctx = new AssOverrideTextAnalyzerContext(eventDurationMs: 1000);
        var analysis = AssOverrideTextAnalyzer.Analyze("{\\move(1,2,3,4,500,1500)}Hi", ctx);
        analysis.Diagnostics.Should().Contain(d => d.Code == "ass.override.timeOutOfRange");
    }

    [TestMethod]
    public void OverrideText_Analyze_WarnsWhenCoordinatesExceedPlayRes()
    {
        var ctx = new AssOverrideTextAnalyzerContext(eventDurationMs: 1000, playResX: 640, playResY: 480);
        var analysis = AssOverrideTextAnalyzer.Analyze("{\\pos(999,999)}Hi", ctx);
        analysis.Diagnostics.Should().Contain(d => d.Code == "ass.override.coordOutOfRange");
    }

    [TestMethod]
    public void OverrideText_Analyze_PrefersLayoutResOverPlayRes()
    {
        var ctx = new AssOverrideTextAnalyzerContext(eventDurationMs: 1000, layoutResX: 320, layoutResY: 240, playResX: 1920, playResY: 1080);
        var analysis = AssOverrideTextAnalyzer.Analyze("{\\pos(400,200)}Hi", ctx);
        analysis.Diagnostics.Should().Contain(d => d.Code == "ass.override.coordOutOfRange");
    }

    [TestMethod]
    public void OverrideText_Analyze_WarnsOnObsoleteAlignmentTag()
    {
        var analysis = AssOverrideTextAnalyzer.Analyze("{\\a6}Hi");
        analysis.Diagnostics.Should().Contain(d => d.Code == "ass.override.obsoleteTag");
    }

    [TestMethod]
    public void OverrideText_Analyze_SuggestsColorNormalization()
    {
        var analysis = AssOverrideTextAnalyzer.Analyze("{\\cFFF}Hi");
        analysis.Diagnostics.Should().Contain(d => d.Code == "ass.override.colorNormalize");
    }

    [TestMethod]
    public void OverrideText_Analyze_ValidatesNestedTransformPayload()
    {
        var analysis = AssOverrideTextAnalyzer.Analyze("{\\t(0,100,\\zz1)}Hi");
        analysis.Diagnostics.Should().Contain(d => d.Code == "ass.override.unknownTag");
    }

    [TestMethod]
    public void OverrideText_Completion_SuggestsOverrideTags()
    {
        var text = "{\\a}";
        var analysis = AssOverrideTextAnalyzer.Analyze(text);
        int col = text.IndexOf("{\\a}", StringComparison.Ordinal) + "{\\a".Length;

        var completions = AssOverrideTextCompletionProvider.GetCompletions(text, new AssPosition(0, col), analysis);
        completions.Items.Should().Contain(i => i.InsertText.Equals("an", StringComparison.OrdinalIgnoreCase));
    }
}
