using System.Text;
using FluentAssertions;
using Mobsub.SubtitleParse.AssText;
using Mobsub.SubtitleParse.AssTypes;

namespace Mobsub.Test;

[TestClass]
public sealed class AssOverrideTagTokenApiTests
{
    [TestMethod]
    public void OverrideTagScanner_EnumeratesKnownAndUnknown()
    {
        using var read = AssEventTextRead.Parse("{\\an7\\zz1\\pos(1,2)}Hi");
        read.TryGetFirstOverrideTagScanner(out var scanner).Should().BeTrue();

        scanner.MoveNext(out var t1).Should().BeTrue();
        t1.IsKnown.Should().BeTrue();
        t1.Tag.Should().Be(AssTag.Alignment);
        Encoding.ASCII.GetString(t1.NameAndMaybePayload).Should().Be("an7");
        Encoding.ASCII.GetString(t1.Param).Should().Be("7");
        AssOverrideTagValueParser.ParseValue(t1, read.Options).TryGet<byte>(out var an).Should().BeTrue();
        an.Should().Be(7);

        scanner.MoveNext(out var t2).Should().BeTrue();
        t2.IsKnown.Should().BeFalse();
        Encoding.ASCII.GetString(t2.NameAndMaybePayload).Should().Be("zz1");

        scanner.MoveNext(out var t3).Should().BeTrue();
        t3.IsKnown.Should().BeTrue();
        t3.Tag.Should().Be(AssTag.Position);
        Encoding.ASCII.GetString(t3.NameAndMaybePayload).Should().Be("pos");

        scanner.MoveNext(out _).Should().BeFalse();
    }

    [TestMethod]
    public void OverrideTagScanner_PrefixConflictProtection_DoesNotSplitModTagWhenDisabled()
    {
        using var read = AssEventTextRead.Parse("{\\blend1}Hi");
        read.TryGetFirstOverrideTagScanner(out var scanner).Should().BeTrue();

        scanner.MoveNext(out var token).Should().BeTrue();
        token.IsKnown.Should().BeFalse();
        Encoding.ASCII.GetString(token.NameAndMaybePayload).Should().Be("blend1");

        scanner.MoveNext(out _).Should().BeFalse();
    }

    [TestMethod]
    public void OverrideTagScanner_ModMode_RecognizesVsFilterModTags()
    {
        var options = new AssTextOptions(AssTextDialect.VsFilterMod);
        using var read = AssEventTextRead.Parse("{\\blend1}Hi", options: options);

        read.Options.Dialect.Should().Be(AssTextDialect.VsFilterMod);
        read.TryGetFirstOverrideTagScanner(out var scanner).Should().BeTrue();

        scanner.MoveNext(out var token).Should().BeTrue();
        token.IsKnown.Should().BeTrue();
        token.Tag.Should().Be(AssTag.BlendMode);
    }
}
