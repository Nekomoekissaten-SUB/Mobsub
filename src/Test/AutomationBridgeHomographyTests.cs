using System.Numerics;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Mobsub.AutomationBridge.Core.Motion;

namespace Mobsub.Test;

[TestClass]
public sealed class AutomationBridgeHomographyTests
{
    [TestMethod]
    public void SquareToQuad_MapsCorners()
    {
        var p0 = new Vector2(10, 20);
        var p1 = new Vector2(110, 20);
        var p2 = new Vector2(100, 70);
        var p3 = new Vector2(20, 80);

        Homography.TryCreateSquareToQuad(p0, p1, p2, p3, out var m)
            .Should().BeTrue();

        AssertClose(p0, Homography.TransformPoint(m, new Vector2(0, 0)));
        AssertClose(p1, Homography.TransformPoint(m, new Vector2(1, 0)));
        AssertClose(p2, Homography.TransformPoint(m, new Vector2(1, 1)));
        AssertClose(p3, Homography.TransformPoint(m, new Vector2(0, 1)));
    }

    [TestMethod]
    public void QuadToSquare_IsInverse()
    {
        var p0 = new Vector2(10, 20);
        var p1 = new Vector2(110, 20);
        var p2 = new Vector2(100, 70);
        var p3 = new Vector2(20, 80);

        Homography.TryCreateSquareToQuad(p0, p1, p2, p3, out var sq2q)
            .Should().BeTrue();
        Homography.TryCreateQuadToSquare(p0, p1, p2, p3, out var q2sq)
            .Should().BeTrue();

        var uv = new Vector2(0.33f, 0.77f);
        var xy = Homography.TransformPoint(sq2q, uv);
        var uv2 = Homography.TransformPoint(q2sq, xy);
        AssertClose(uv, uv2, eps: 1e-3f);
    }

    private static void AssertClose(Vector2 expected, Vector2 actual, float eps = 1e-2f)
    {
        actual.X.Should().BeApproximately(expected.X, eps);
        actual.Y.Should().BeApproximately(expected.Y, eps);
    }
}

