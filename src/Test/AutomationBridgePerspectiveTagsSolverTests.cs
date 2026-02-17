using System.Numerics;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Mobsub.AutomationBridge.Core.Motion;

namespace Mobsub.Test;

[TestClass]
public sealed class AutomationBridgePerspectiveTagsSolverTests
{
    [TestMethod]
    public void SolveFromQuad_RoundTrips_OrgCenter()
    {
        var quad = new[]
        {
            new Vector2(100, 100),
            new Vector2(300, 80),
            new Vector2(320, 160),
            new Vector2(90, 180),
        };

        const double width = 200;
        const double height = 50;

        PerspectiveTagsSolver.TrySolveFromQuad(
                quad,
                width,
                height,
                align: 7,
                orgMode: 2,
                origin: default,
                layoutScale: 1,
                out var tags)
            .Should().BeTrue();

        Span<Vector2> roundTrip = stackalloc Vector2[4];
        PerspectiveTagsSolver.TransformRect(tags, width, height, roundTrip, layoutScale: 1);

        AssertCloseQuad(quad, roundTrip, eps: 0.2f);
    }

    [TestMethod]
    public void SolveFromQuad_RoundTrips_KeepOrigin()
    {
        var quad = new[]
        {
            new Vector2(50, 40),
            new Vector2(260, 30),
            new Vector2(280, 140),
            new Vector2(30, 160),
        };

        const double width = 180;
        const double height = 60;

        var origin = new Vector2(123.4f, 56.7f);

        PerspectiveTagsSolver.TrySolveFromQuad(
                quad,
                width,
                height,
                align: 5,
                orgMode: 1,
                origin: origin,
                layoutScale: 1,
                out var tags)
            .Should().BeTrue();

        Span<Vector2> roundTrip = stackalloc Vector2[4];
        PerspectiveTagsSolver.TransformRect(tags, width, height, roundTrip, layoutScale: 1);

        AssertCloseQuad(quad, roundTrip, eps: 0.2f);
    }

    [TestMethod]
    public void SolveFromQuad_RoundTrips_OrgMode3()
    {
        var quad = new[]
        {
            new Vector2(180, 120),
            new Vector2(420, 100),
            new Vector2(400, 260),
            new Vector2(160, 280),
        };

        const double width = 240;
        const double height = 90;

        PerspectiveTagsSolver.TrySolveFromQuad(
                quad,
                width,
                height,
                align: 7,
                orgMode: 3,
                origin: default,
                layoutScale: 0.9,
                out var tags)
            .Should().BeTrue();

        Span<Vector2> roundTrip = stackalloc Vector2[4];
        PerspectiveTagsSolver.TransformRect(tags, width, height, roundTrip, layoutScale: 0.9);

        AssertCloseQuad(quad, roundTrip, eps: 0.25f);
    }

    private static void AssertCloseQuad(IReadOnlyList<Vector2> expected, ReadOnlySpan<Vector2> actual, float eps)
    {
        actual.Length.Should().BeGreaterThanOrEqualTo(4);
        AssertClose(expected[0], actual[0], eps);
        AssertClose(expected[1], actual[1], eps);
        AssertClose(expected[2], actual[2], eps);
        AssertClose(expected[3], actual[3], eps);
    }

    private static void AssertClose(Vector2 expected, Vector2 actual, float eps)
    {
        actual.X.Should().BeApproximately(expected.X, eps);
        actual.Y.Should().BeApproximately(expected.Y, eps);
    }
}
