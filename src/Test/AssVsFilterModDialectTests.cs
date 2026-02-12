using FluentAssertions;
using Mobsub.SubtitleParse.AssText;
using Mobsub.SubtitleParse.AssTypes;

namespace Mobsub.Test;

[TestClass]
public class AssVsFilterModDialectTests
{
    private static AssTextOptions ModOptions => new(Dialect: AssTextDialect.VsFilterMod);

    [TestMethod]
    public void VsFilterMod_Disabled_Blend_IsUnknown_AndNotBold()
    {
        ReadOnlyMemory<byte> line = "{\\blend1}x"u8.ToArray();

        var segments = AssEventTextParser.ParseLine(line).Span;
        segments[0].SegmentKind.Should().Be(AssEventSegmentKind.TagBlock);
        segments[0].Tags.Should().NotBeNull();
        segments[0].Tags!.Value.Span.IsEmpty.Should().BeTrue();

        using var read = AssEventTextRead.Parse(line);
        var issues = new List<AssOverrideValidationIssue>();
        AssOverrideTagValidator.ValidateOverrideBlocks(read.Utf8, read.Segments, issues);

        issues.Should().Contain(i => i.Code == "ass.override.unknownTag");
        issues.Should().NotContain(i => i.Code == "ass.override.vsfiltermodTag");
    }

    [TestMethod]
    public void VsFilterMod_Enabled_Blend_IsRecognized_AndWarned()
    {
        ReadOnlyMemory<byte> line = "{\\blend1}x"u8.ToArray();
        var options = ModOptions;

        var segments = AssEventTextParser.ParseLine(line, options).Span;
        var tags = segments[0].Tags!.Value.Span;
        tags.Length.Should().Be(1);
        tags[0].Tag.Should().Be(AssTag.BlendMode);
        tags[0].TryGet<ReadOnlyMemory<byte>>(out var payload).Should().BeTrue();
        payload.Span.SequenceEqual("1"u8).Should().BeTrue();

        using var read = AssEventTextRead.Parse(line, options);
        var issues = new List<AssOverrideValidationIssue>();
        AssOverrideTagValidator.ValidateOverrideBlocks(read.Utf8, read.Segments, issues, options: options);

        issues.Should().Contain(i => i.Code == "ass.override.vsfiltermodTag");
        issues.Should().NotContain(i => i.Code == "ass.override.unknownTag");
    }

    [TestMethod]
    public void VsFilterMod_Enabled_Blend_UnknownKeyword_IsWarned()
    {
        ReadOnlyMemory<byte> line = "{\\blendwat}x"u8.ToArray();
        var options = ModOptions;

        using var read = AssEventTextRead.Parse(line, options);
        var issues = new List<AssOverrideValidationIssue>();
        AssOverrideTagValidator.ValidateOverrideBlocks(read.Utf8, read.Segments, issues, options: options);

        issues.Should().Contain(i => i.Code == "ass.override.keywordUnknown");
    }

    [TestMethod]
    public void VsFilterMod_Enabled_RndsHex_IsParsed()
    {
        ReadOnlyMemory<byte> line = "{\\rndsFF}x"u8.ToArray();
        var options = ModOptions;

        var segments = AssEventTextParser.ParseLine(line, options).Span;
        var tags = segments[0].Tags!.Value.Span;
        tags.Length.Should().Be(1);
        tags[0].Tag.Should().Be(AssTag.RandomSeed);
        tags[0].TryGet<int>(out var seed).Should().BeTrue();
        seed.Should().Be(255);
    }

    [TestMethod]
    public void VsFilterMod_Enabled_RndsInvalidHex_IsReported()
    {
        ReadOnlyMemory<byte> line = "{\\rndsGG}x"u8.ToArray();
        var options = ModOptions;

        using var read = AssEventTextRead.Parse(line, options);
        var issues = new List<AssOverrideValidationIssue>();
        AssOverrideTagValidator.ValidateOverrideBlocks(read.Utf8, read.Segments, issues, options: options);

        issues.Should().Contain(i => i.Code == "ass.override.hexLoose");
    }

    [TestMethod]
    public void VsFilterMod_Strictness_ControlsModTagSeverity()
    {
        ReadOnlyMemory<byte> line = "{\\blend1}x"u8.ToArray();

        var compat = new AssTextOptions(Dialect: AssTextDialect.VsFilterMod, Strictness: AssValidationStrictness.Compat);
        using (var read = AssEventTextRead.Parse(line, compat))
        {
            var issues = new List<AssOverrideValidationIssue>();
            AssOverrideTagValidator.ValidateOverrideBlocks(read.Utf8, read.Segments, issues, options: compat);
            issues.Should().Contain(i => i.Code == "ass.override.vsfiltermodTag" && i.Severity == AssOverrideValidationSeverity.Info);
        }

        var strict = new AssTextOptions(Dialect: AssTextDialect.VsFilterMod, Strictness: AssValidationStrictness.Strict);
        using (var read = AssEventTextRead.Parse(line, strict))
        {
            var issues = new List<AssOverrideValidationIssue>();
            AssOverrideTagValidator.ValidateOverrideBlocks(read.Utf8, read.Segments, issues, options: strict);
            issues.Should().Contain(i => i.Code == "ass.override.vsfiltermodTag" && i.Severity == AssOverrideValidationSeverity.Error);
        }
    }

    [TestMethod]
    public void VsFilterMod_Disabled_MoveVC_IsUnknown()
    {
        ReadOnlyMemory<byte> line = "{\\movevc(1,2)}x"u8.ToArray();

        var segments = AssEventTextParser.ParseLine(line).Span;
        segments[0].Tags!.Value.Span.IsEmpty.Should().BeTrue();

        using var read = AssEventTextRead.Parse(line);
        var issues = new List<AssOverrideValidationIssue>();
        AssOverrideTagValidator.ValidateOverrideBlocks(read.Utf8, read.Segments, issues);

        issues.Should().Contain(i => i.Code == "ass.override.unknownTag");
        issues.Should().NotContain(i => i.Code == "ass.override.functionInvalid");
    }

    [TestMethod]
    public void VsFilterMod_Enabled_MoveVC_IsRecognized()
    {
        ReadOnlyMemory<byte> line = "{\\movevc(1,2)}x"u8.ToArray();
        var options = ModOptions;

        var segments = AssEventTextParser.ParseLine(line, options).Span;
        var tags = segments[0].Tags!.Value.Span;
        tags.Length.Should().Be(1);
        tags[0].Tag.Should().Be(AssTag.MoveVC);

        using var read = AssEventTextRead.Parse(line, options);
        var issues = new List<AssOverrideValidationIssue>();
        AssOverrideTagValidator.ValidateOverrideBlocks(read.Utf8, read.Segments, issues, options: options);

        issues.Should().Contain(i => i.Code == "ass.override.vsfiltermodTag");
        issues.Should().NotContain(i => i.Code == "ass.override.unknownTag");
        issues.Should().NotContain(i => i.Code == "ass.override.functionInvalid");
    }

    [TestMethod]
    public void VsFilterMod_Pos3_IsValidOnlyWhenEnabled()
    {
        ReadOnlyMemory<byte> line = "{\\pos(1,2,3)}x"u8.ToArray();

        var segments = AssEventTextParser.ParseLine(line).Span;
        var tags = segments[0].Tags!.Value.Span;
        tags.Length.Should().Be(1);
        tags[0].Tag.Should().Be(AssTag.Position);
        tags[0].TryGet<AssTagFunctionValue>(out _).Should().BeFalse();

        using (var read = AssEventTextRead.Parse(line))
        {
            var issues = new List<AssOverrideValidationIssue>();
            AssOverrideTagValidator.ValidateOverrideBlocks(read.Utf8, read.Segments, issues);
            issues.Should().Contain(i => i.Code == "ass.override.functionInvalid");
            issues.Should().NotContain(i => i.Code == "ass.override.vsfiltermodOverload");
        }

        var options = ModOptions;
        using (var read = AssEventTextRead.Parse(line, options))
        {
            var issues = new List<AssOverrideValidationIssue>();
            AssOverrideTagValidator.ValidateOverrideBlocks(read.Utf8, read.Segments, issues, options: options);
            issues.Should().NotContain(i => i.Code == "ass.override.functionInvalid");
            issues.Should().Contain(i => i.Code == "ass.override.vsfiltermodOverload");
        }
    }

    [TestMethod]
    public void VsFilterMod_FscPayload_ProducesExpectedWarnings()
    {
        ReadOnlyMemory<byte> line = "{\\fsc50}x"u8.ToArray();

        using (var read = AssEventTextRead.Parse(line))
        {
            var issues = new List<AssOverrideValidationIssue>();
            AssOverrideTagValidator.ValidateOverrideBlocks(read.Utf8, read.Segments, issues);
            issues.Should().Contain(i => i.Code == "ass.override.nonStandardPayload");
            issues.Should().NotContain(i => i.Code == "ass.override.vsfiltermodOverload");
        }

        var options = ModOptions;
        using (var read = AssEventTextRead.Parse(line, options))
        {
            var issues = new List<AssOverrideValidationIssue>();
            AssOverrideTagValidator.ValidateOverrideBlocks(read.Utf8, read.Segments, issues, options: options);
            issues.Should().Contain(i => i.Code == "ass.override.vsfiltermodOverload");
        }
    }
}
