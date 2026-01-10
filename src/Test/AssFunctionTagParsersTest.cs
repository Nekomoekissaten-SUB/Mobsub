using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Mobsub.SubtitleParseNT2.AssUtils;

namespace Mobsub.Test;

[TestClass]
public class AssFunctionTagParsersTest
{
    [TestMethod]
    public void Pos_AllowsSurroundingSpaces()
    {
        AssFunctionTagParsers.TryParsePos("  ( 1 , 2 )  "u8, out var x, out var y)
            .Should().BeTrue();
        x.Should().BeApproximately(1, 1e-12);
        y.Should().BeApproximately(2, 1e-12);
    }

    [TestMethod]
    public void Pos_InvalidForms_AreRejected()
    {
        AssFunctionTagParsers.TryParsePos("(1 2)"u8, out _, out _).Should().BeFalse();
        AssFunctionTagParsers.TryParsePos("(1,)"u8, out _, out _).Should().BeFalse();
        AssFunctionTagParsers.TryParsePos("(,2)"u8, out _, out _).Should().BeFalse();
        AssFunctionTagParsers.TryParsePos("(1,2,3)"u8, out _, out _).Should().BeFalse();
    }

    [TestMethod]
    public void Org_ParsesLikePos()
    {
        AssFunctionTagParsers.TryParseOrg("(1.5,2.5)"u8, out var x, out var y)
            .Should().BeTrue();
        x.Should().BeApproximately(1.5, 1e-12);
        y.Should().BeApproximately(2.5, 1e-12);
    }

    [TestMethod]
    public void ClipRect_AllowsFloats_Truncates()
    {
        AssFunctionTagParsers.TryParseClipRect("(1.9, 2.1, 100.9, 200.0)"u8, out var x1, out var y1, out var x2, out var y2)
            .Should().BeTrue();

        x1.Should().Be(1);
        y1.Should().Be(2);
        x2.Should().Be(100);
        y2.Should().Be(200);
    }

    [TestMethod]
    public void Clip_DistinguishesRectVsDrawing()
    {
        AssFunctionTagParsers.TryParseClip("(1,2,3,4)"u8,
            out var kind,
            out var x1, out var y1, out var x2, out var y2,
            out var scale, out var drawing).Should().BeTrue();

        kind.Should().Be(AssFunctionTagParsers.AssClipKind.Rect);
        (x1, y1, x2, y2).Should().Be((1, 2, 3, 4));
        scale.Should().Be(1);
        drawing.IsEmpty.Should().BeTrue();

        AssFunctionTagParsers.TryParseClip("(2, m 0 0 l 10 10)"u8,
            out kind,
            out x1, out y1, out x2, out y2,
            out scale, out drawing).Should().BeTrue();

        kind.Should().Be(AssFunctionTagParsers.AssClipKind.Drawing);
        scale.Should().Be(2);
        drawing.SequenceEqual("m 0 0 l 10 10"u8).Should().BeTrue();
    }

    [TestMethod]
    public void ClipDrawing_WithScaleAndCommaSeparatedArgs_IsRejected()
    {
        AssFunctionTagParsers.TryParseClipDrawing("(1,2,3,4)"u8, out _, out _)
            .Should().BeFalse();
    }

    [TestMethod]
    public void ClipDrawing_WithCommaSeparatedPayload_IsRejected()
    {
        AssFunctionTagParsers.TryParseClipDrawing("(2, m 0 0, l 10 10)"u8, out _, out _)
            .Should().BeFalse();
    }

    [TestMethod]
    public void ClipDrawing_AllowsScaleAsFloatInteger()
    {
        AssFunctionTagParsers.TryParseClipDrawing("(2.0, m 0 0 l 10 10)"u8, out var scale, out var drawing)
            .Should().BeTrue();
        scale.Should().Be(2);
        drawing.SequenceEqual("m 0 0 l 10 10"u8).Should().BeTrue();
    }

    [TestMethod]
    public void ClipDrawing_AllowsScaleAsNonIntegerFloat_Truncates()
    {
        AssFunctionTagParsers.TryParseClipDrawing("(2.5, m 0 0 l 10 10)"u8, out var scale, out var drawing)
            .Should().BeTrue();
        scale.Should().Be(2);
        drawing.SequenceEqual("m 0 0 l 10 10"u8).Should().BeTrue();
    }

    [TestMethod]
    public void ClipDrawing_AllowsNumericPayloadWithoutCommands()
    {
        AssFunctionTagParsers.TryParseClipDrawing("(0 0 10 10)"u8, out var scale, out var drawing)
            .Should().BeTrue();
        scale.Should().Be(1);
        drawing.SequenceEqual("0 0 10 10"u8).Should().BeTrue();
    }

    [TestMethod]
    public void Transform_AccelForm_WithTrailingComma()
    {
        AssFunctionTagParsers.TryParseTransform("(0.5,\\bord2)"u8,
            out var t1, out var t2, out var hasTimes,
            out var accel, out var hasAccel,
            out var tagPayload).Should().BeTrue();

        hasTimes.Should().BeFalse();
        hasAccel.Should().BeTrue();
        accel.Should().BeApproximately(0.5, 1e-12);
        tagPayload.SequenceEqual("\\bord2"u8).Should().BeTrue();
        t1.Should().Be(0);
        t2.Should().Be(0);
    }

    [TestMethod]
    public void Transform_TimesForm_WithTrailingComma()
    {
        AssFunctionTagParsers.TryParseTransform("(0,100,\\bord2)"u8,
            out var t1, out var t2, out var hasTimes,
            out var accel, out var hasAccel,
            out var tagPayload).Should().BeTrue();

        hasTimes.Should().BeTrue();
        t1.Should().Be(0);
        t2.Should().Be(100);
        hasAccel.Should().BeFalse();
        tagPayload.SequenceEqual("\\bord2"u8).Should().BeTrue();
        accel.Should().Be(0);
    }

    [TestMethod]
    public void Transform_TimesAndAccelForm_WithTrailingComma()
    {
        AssFunctionTagParsers.TryParseTransform("(0,100,2,\\bord2)"u8,
            out var t1, out var t2, out var hasTimes,
            out var accel, out var hasAccel,
            out var tagPayload).Should().BeTrue();

        hasTimes.Should().BeTrue();
        t1.Should().Be(0);
        t2.Should().Be(100);
        hasAccel.Should().BeTrue();
        accel.Should().BeApproximately(2, 1e-12);
        tagPayload.SequenceEqual("\\bord2"u8).Should().BeTrue();
    }

    [TestMethod]
    public void Transform_TimesForm_AllowsFloatStart_Truncates()
    {
        AssFunctionTagParsers.TryParseTransform("(0.5,100,\\bord2)"u8,
            out var t1, out var t2, out var hasTimes,
            out var accel, out var hasAccel,
            out var tagPayload).Should().BeTrue();

        hasTimes.Should().BeTrue();
        t1.Should().Be(0);
        t2.Should().Be(100);
        hasAccel.Should().BeFalse();
        accel.Should().Be(0);
        tagPayload.SequenceEqual("\\bord2"u8).Should().BeTrue();
    }

    [TestMethod]
    public void Transform_InvalidHeader_NonNumeric_IsTreatedAsZero()
    {
        AssFunctionTagParsers.TryParseTransform("(abc,\\bord2)"u8,
            out var t1, out var t2, out var hasTimes,
            out var accel, out var hasAccel,
            out var tagPayload).Should().BeTrue();

        hasTimes.Should().BeFalse();
        hasAccel.Should().BeTrue();
        accel.Should().Be(0);
        t1.Should().Be(0);
        t2.Should().Be(0);
        tagPayload.SequenceEqual("\\bord2"u8).Should().BeTrue();
    }

    [TestMethod]
    public void Transform_InvalidHeader_TooManyTokens_IsRejected()
    {
        AssFunctionTagParsers.TryParseTransform("(0,100,2,3,\\bord2)"u8,
            out _, out _, out _,
            out _, out _,
            out _).Should().BeFalse();
    }

    [TestMethod]
    public void Move_AllowsFloatTimes_Truncates()
    {
        AssFunctionTagParsers.TryParseMove("(0,0,100,200, 12.9, 34.1)"u8,
            out var x1, out var y1, out var x2, out var y2,
            out var t1, out var t2, out var hasTimes).Should().BeTrue();

        (x1, y1, x2, y2).Should().Be((0d, 0d, 100d, 200d));
        hasTimes.Should().BeTrue();
        t1.Should().Be(12);
        t2.Should().Be(34);
    }

    [TestMethod]
    public void Move_WithoutTimes_IsAccepted()
    {
        AssFunctionTagParsers.TryParseMove("(0,0,100,200)"u8,
            out _, out _, out _, out _,
            out _, out _, out var hasTimes).Should().BeTrue();
        hasTimes.Should().BeFalse();
    }

    [TestMethod]
    public void Move_InvalidForms_AreRejected()
    {
        // Missing t2
        AssFunctionTagParsers.TryParseMove("(0,0,1,1, 10)"u8,
            out _, out _, out _, out _,
            out _, out _, out _).Should().BeFalse();

        // Extra token after t2
        AssFunctionTagParsers.TryParseMove("(0,0,1,1, 10, 20, 30)"u8,
            out _, out _, out _, out _,
            out _, out _, out _).Should().BeFalse();
    }

    [TestMethod]
    public void Fad_AllowsFloatTimes_Truncates()
    {
        AssFunctionTagParsers.TryParseFad("( 10.9 , 20.1 )"u8, out var t1, out var t2)
            .Should().BeTrue();
        t1.Should().Be(10);
        t2.Should().Be(20);
    }

    [TestMethod]
    public void Fad_InvalidForms_AreRejected()
    {
        AssFunctionTagParsers.TryParseFad("(10)"u8, out _, out _).Should().BeFalse();
        AssFunctionTagParsers.TryParseFad("(10,)"u8, out _, out _).Should().BeFalse();
        AssFunctionTagParsers.TryParseFad("(,20)"u8, out _, out _).Should().BeFalse();
        AssFunctionTagParsers.TryParseFad("(10,20,30)"u8, out _, out _).Should().BeFalse();
        AssFunctionTagParsers.TryParseFad("(10,20,)"u8, out _, out _).Should().BeTrue();
    }

    [TestMethod]
    public void Fade_AllowsFloatTokens_Truncates()
    {
        AssFunctionTagParsers.TryParseFade("(0.0, 255.9, 128.1, 0, 100.1, 200.9, 300.0)"u8,
            out var a1, out var a2, out var a3,
            out var t1, out var t2, out var t3, out var t4).Should().BeTrue();

        a1.Should().Be(0);
        a2.Should().Be(255);
        a3.Should().Be(128);
        t1.Should().Be(0);
        t2.Should().Be(100);
        t3.Should().Be(200);
        t4.Should().Be(300);
    }

    [TestMethod]
    public void Fade_InvalidForms_AreRejected()
    {
        // missing tokens
        AssFunctionTagParsers.TryParseFade("(0,1,2,3,4,5)"u8,
            out _, out _, out _, out _, out _, out _, out _).Should().BeFalse();

        // extra token
        AssFunctionTagParsers.TryParseFade("(0,1,2,3,4,5,6,7)"u8,
            out _, out _, out _, out _, out _, out _, out _).Should().BeFalse();

        AssFunctionTagParsers.TryParseFade("(0,1,2,3,4,5,6,)"u8,
            out _, out _, out _, out _, out _, out _, out _).Should().BeTrue();
    }
}
