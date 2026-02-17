using FluentAssertions;
using Mobsub.SubtitleParse.AssText;

namespace Mobsub.Test;

[TestClass]
public class AssOverrideTagOptimizerTests
{
    [TestMethod]
    public void Optimize_RemovesOverwrittenTagsInSameBlock()
    {
        byte[] input = "{\\bord1\\bord2}x"u8.ToArray();
        byte[] expected = "{\\bord2}x"u8.ToArray();

        byte[] actual = AssOverrideTagOptimizer.Optimize(input);

        actual.Should().Equal(expected);
    }

    [TestMethod]
    public void Optimize_RemovesDuplicateLineOnlyRenderFirstTags()
    {
        byte[] input = "{\\pos(1,2)\\pos(3,4)}x"u8.ToArray();
        byte[] expected = "{\\pos(1,2)}x"u8.ToArray();

        byte[] actual = AssOverrideTagOptimizer.Optimize(input);

        actual.Should().Equal(expected);
    }

    [TestMethod]
    public void Optimize_OnlyKeepsLastLineOnlyRenderLatestTag()
    {
        byte[] input = "{\\q0}a{\\q1}b"u8.ToArray();
        byte[] expected = "a{\\q1}b"u8.ToArray();

        byte[] actual = AssOverrideTagOptimizer.Optimize(input);

        actual.Should().Equal(expected);
    }

    [TestMethod]
    public void Optimize_RemovesNoOpAssignmentsAcrossBlocks()
    {
        byte[] input = "{\\bord2}a{\\bord2}b"u8.ToArray();
        byte[] expected = "{\\bord2}ab"u8.ToArray();

        byte[] actual = AssOverrideTagOptimizer.Optimize(input);

        actual.Should().Equal(expected);
    }

    [TestMethod]
    public void Optimize_DoesNotRemoveAssignmentsAfterReset()
    {
        byte[] input = "{\\bord2\\r\\bord2}x"u8.ToArray();

        byte[] actual = AssOverrideTagOptimizer.Optimize(input);

        actual.Should().Equal(input);
    }

    [TestMethod]
    public void Optimize_OptimizesTransformPayload()
    {
        byte[] input = "{\\t(0,100,\\bord1\\bord2)}x"u8.ToArray();
        byte[] expected = "{\\t(0,100,\\bord2)}x"u8.ToArray();

        byte[] actual = AssOverrideTagOptimizer.Optimize(input);

        actual.Should().Equal(expected);
    }
}

