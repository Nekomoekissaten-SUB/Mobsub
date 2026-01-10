using FluentAssertions;
using Mobsub.SubtitleParseNT2.AssTypes;
using Mobsub.SubtitleParseNT2.AssUtils;

namespace Mobsub.Test;

[TestClass]
public class AssTagBoolParseTest
{
    [DataTestMethod]
    [DataRow("{\\i-1}x", AssTag.Italic)]
    [DataRow("{\\u-1}x", AssTag.Underline)]
    [DataRow("{\\s-1}x", AssTag.Strikeout)]
    public void ParseLine_BoolTagsWithMinusOne_AreIgnored(string lineText, AssTag expectedTag)
    {
        ReadOnlyMemory<byte> line = System.Text.Encoding.UTF8.GetBytes(lineText);
        var segments = AssEventParser.ParseLine(line).Span;

        segments.Length.Should().Be(2);
        segments[0].SegmentKind.Should().Be(AssEventSegmentKind.TagBlock);

        var tags = segments[0].Tags!.Value.Span;
        tags.Length.Should().Be(1);
        tags[0].Tag.Should().Be(expectedTag);
        tags[0].TryGet<bool>(out _).Should().BeFalse();
        tags[0].Value.Kind.Should().Be(AssTagValueKind.None);
    }

    [DataTestMethod]
    [DataRow("{\\i1}x", AssTag.Italic, true)]
    [DataRow("{\\i0}x", AssTag.Italic, false)]
    [DataRow("{\\u1}x", AssTag.Underline, true)]
    [DataRow("{\\u0}x", AssTag.Underline, false)]
    [DataRow("{\\s1}x", AssTag.Strikeout, true)]
    [DataRow("{\\s0}x", AssTag.Strikeout, false)]
    public void ParseLine_BoolTagsWithZeroOrOne_ParseCorrectly(string lineText, AssTag expectedTag, bool expectedValue)
    {
        ReadOnlyMemory<byte> line = System.Text.Encoding.UTF8.GetBytes(lineText);
        var segments = AssEventParser.ParseLine(line).Span;

        segments.Length.Should().Be(2);
        segments[0].SegmentKind.Should().Be(AssEventSegmentKind.TagBlock);

        var tags = segments[0].Tags!.Value.Span;
        tags.Length.Should().Be(1);
        tags[0].Tag.Should().Be(expectedTag);
        tags[0].TryGet<bool>(out var value).Should().BeTrue();
        value.Should().Be(expectedValue);
    }
}
