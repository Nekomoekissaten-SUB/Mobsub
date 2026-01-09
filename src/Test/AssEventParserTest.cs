using FluentAssertions;
using Mobsub.SubtitleParseNT2.AssTypes;
using Mobsub.SubtitleParseNT2.AssUtils;

namespace Mobsub.Test;

[TestClass]
public class AssEventParserTest
{
    [TestMethod]
    public void ParseLine_DoesNotDuplicateSegments()
    {
        ReadOnlyMemory<byte> line = "Hello{\\b1}World"u8.ToArray();

        var segments = AssEventParser.ParseLine(line).Span;

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
}
