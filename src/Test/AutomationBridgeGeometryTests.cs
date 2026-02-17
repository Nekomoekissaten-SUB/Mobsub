using System.Numerics;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Mobsub.AutomationBridge.Core.Ass;
using Mobsub.AutomationBridge.Core.Geometry;

namespace Mobsub.Test;

[TestClass]
public sealed class AutomationBridgeGeometryTests
{
    [TestMethod]
    public void BezierFlatten_IncludesEndpoints()
    {
        var bez = new CubicBezier(
            new Vector2(0, 0),
            new Vector2(0, 100),
            new Vector2(100, 100),
            new Vector2(100, 0));

        var pts = new List<Vector2>();
        bez.Flatten(tolerance: 1.0, pts);

        pts.Count.Should().BeGreaterThanOrEqualTo(2);
        pts[0].Should().Be(bez.P0);
        pts[^1].Should().Be(bez.P3);
    }

    [TestMethod]
    public void RdpSimplifier_ReducesCollinearPoints()
    {
        var pts = new[]
        {
            new Vector2(0, 0),
            new Vector2(10, 0),
            new Vector2(20, 0),
            new Vector2(30, 0),
        };

        var outPts = new List<Vector2>();
        RdpSimplifier.Simplify(pts, tolerance: 0.01, outPts);

        outPts.Should().HaveCount(2);
        outPts[0].Should().Be(new Vector2(0, 0));
        outPts[1].Should().Be(new Vector2(30, 0));
    }

    [TestMethod]
    public void AssDrawingOptimizer_FlattensBezierToLine()
    {
        // Simple cubic curve.
        string input = "m 0 0 b 0 50 50 50 50 0";
        string output = AssDrawingOptimizer.OptimizeDrawing(input, curveTolerance: 1.0, simplifyTolerance: 0, precisionDecimals: 0);

        // Output is expected to contain a lineto and must not contain "b".
        output.Should().Contain("l");
        output.Should().NotContain(" b ");
        output.Should().NotStartWith("b");
    }
}
