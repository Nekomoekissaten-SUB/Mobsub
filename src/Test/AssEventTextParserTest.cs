using FluentAssertions;
using Mobsub.SubtitleParse.AssText;
using Mobsub.SubtitleParse.AssTypes;


namespace Mobsub.Test;

[TestClass]
public class AssEventTextParserTest
{
    [TestMethod]
    public void ParseLine_DoesNotDuplicateSegments()
    {
        ReadOnlyMemory<byte> line = "Hello{\\b1}World"u8.ToArray();

        var segments = AssEventTextParser.ParseLine(line).Span;

        segments.Length.Should().Be(3);
        segments[0].SegmentKind.Should().Be(AssEventSegmentKind.Text);
        segments[1].SegmentKind.Should().Be(AssEventSegmentKind.TagBlock);
        segments[2].SegmentKind.Should().Be(AssEventSegmentKind.Text);

        segments[0].LineRange.Should().Be(new Range(0, 5));
        segments[1].LineRange.Should().Be(new Range(5, 10));
        segments[2].LineRange.Should().Be(new Range(10, 15));

        segments[1].Tags.Should().NotBeNull();
        var tags = segments[1].Tags!.Value.Span;
        tags.Length.Should().Be(1);
        tags[0].Tag.Should().Be(AssTag.Bold);
        tags[0].TryGet<int>(out var bold).Should().BeTrue();
        bold.Should().Be(1);
    }

    [TestMethod]
    public void ParseLine_AllowsSpaceBeforeFunctionParen()
    {
        ReadOnlyMemory<byte> line = "{\\t (0.5,100,\\pos(1,2))}x"u8.ToArray();

        var segments = AssEventTextParser.ParseLine(line).Span;

        segments.Length.Should().Be(2);
        segments[0].SegmentKind.Should().Be(AssEventSegmentKind.TagBlock);

        var tags = segments[0].Tags!.Value.Span;
        tags.Length.Should().Be(1);
        tags[0].Tag.Should().Be(AssTag.Transform);
        tags[0].TryGet<AssTagFunctionValue>(out var value).Should().BeTrue();
        value.Kind.Should().Be(AssTagFunctionKind.Transform);
        value.HasTimes.Should().BeTrue();
        value.T1.Should().Be(0);
        value.T2.Should().Be(100);
        value.HasAccel.Should().BeFalse();
        value.TagPayload.Span.SequenceEqual("\\pos(1,2)"u8).Should().BeTrue();
    }

    [TestMethod]
    public void ParseLine_ParsesRawHexColorAndAlpha()
    {
        ReadOnlyMemory<byte> line = "{\\c0000FF\\alpha7F}x"u8.ToArray();

        var segments = AssEventTextParser.ParseLine(line).Span;

        segments.Length.Should().Be(2);
        segments[0].SegmentKind.Should().Be(AssEventSegmentKind.TagBlock);

        var tags = segments[0].Tags!.Value.Span;
        tags.Length.Should().Be(2);
        tags[0].Tag.Should().Be(AssTag.ColorPrimaryAbbreviation);
        tags[0].TryGet<AssColor32>(out var color).Should().BeTrue();
        color.R.Should().Be(0xFF);
        color.G.Should().Be(0x00);
        color.B.Should().Be(0x00);

        tags[1].Tag.Should().Be(AssTag.Alpha);
        tags[1].TryGet<byte>(out var alpha).Should().BeTrue();
        alpha.Should().Be(0x7F);
    }

    [TestMethod]
    public void ParseLine_IgnoresHighByteInColorTag()
    {
        ReadOnlyMemory<byte> line = "{\\c11223344}x"u8.ToArray();

        var segments = AssEventTextParser.ParseLine(line).Span;

        segments.Length.Should().Be(2);
        segments[0].SegmentKind.Should().Be(AssEventSegmentKind.TagBlock);

        var tags = segments[0].Tags!.Value.Span;
        tags.Length.Should().Be(1);
        tags[0].Tag.Should().Be(AssTag.ColorPrimaryAbbreviation);
        tags[0].TryGet<AssColor32>(out var color).Should().BeTrue();
        color.R.Should().Be(0x44);
        color.G.Should().Be(0x33);
        color.B.Should().Be(0x22);
    }

    [TestMethod]
    public void ParseLine_ParsesKaraokeTAndFontScaleReset()
    {
        ReadOnlyMemory<byte> line = "{\\kt10\\fsc50}x"u8.ToArray();

        var segments = AssEventTextParser.ParseLine(line).Span;

        segments.Length.Should().Be(2);
        segments[0].SegmentKind.Should().Be(AssEventSegmentKind.TagBlock);

        var tags = segments[0].Tags!.Value.Span;
        tags.Length.Should().Be(2);
        tags[0].Tag.Should().Be(AssTag.KaraokeT);
        tags[0].TryGet<int>(out var kt).Should().BeTrue();
        kt.Should().Be(10);

        tags[1].Tag.Should().Be(AssTag.FontScale);
        tags[1].TryGet<double>(out var fsc).Should().BeTrue();
        fsc.Should().Be(50);
    }
}
