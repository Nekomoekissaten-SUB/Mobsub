using System.Diagnostics;
using System.Globalization;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Mobsub.AutomationBridge.Dispatch;
using Mobsub.AutomationBridge.Protocol;

namespace Mobsub.Test;

[TestClass]
public sealed class AutomationBridgeBenchmarks
{
    [TestMethod]
    [Ignore("Manual benchmark; not intended for CI.")]
    public void Bench_BridgeDispatcher_DrawingOptimizeLines()
    {
        var line = new BridgeLine(
            Index: 1,
            Class: "dialogue",
            TextUtf8: "{\\p1}m 0 0 b 0 0 10 0 10 10"u8.ToArray(),
            Raw: null,
            StartTime: null,
            EndTime: null,
            StartFrame: null,
            EndFrame: null,
            Layer: null,
            Comment: null,
            Style: null,
            Actor: null,
            Effect: null,
            MarginL: null,
            MarginR: null,
            MarginT: null,
            Extra: null,
            Width: null,
            Height: null,
            Align: null);

        var call = new DrawingOptimizeLinesCall(
            Lines: [line],
            Args: new DrawingOptimizeLinesArgs(CurveTolerance: 0.25, SimplifyTolerance: 0.1, PrecisionDecimals: 0));

        byte[] payload = BridgeMessagePack.SerializeRequest(new BridgeRequest(BridgeMessagePack.SchemaVersion, call));
        byte[] wrapped = BridgeEnvelope.Wrap(payload);

        for (int i = 0; i < 32; i++)
            BridgeDispatcher.Invoke(wrapped);

        long allocBefore = GC.GetAllocatedBytesForCurrentThread();
        int gen0Before = GC.CollectionCount(0);

        const int N = 512;
        var sw = Stopwatch.StartNew();
        BridgeDispatcher.InvokeResult last = default;
        for (int i = 0; i < N; i++)
            last = BridgeDispatcher.Invoke(wrapped);
        sw.Stop();

        long allocAfter = GC.GetAllocatedBytesForCurrentThread();
        int gen0After = GC.CollectionCount(0);

        Console.WriteLine($"Bench_BridgeDispatcher_DrawingOptimizeLines: n={N} time_ms={sw.Elapsed.TotalMilliseconds:F2} alloc_bytes={allocAfter - allocBefore} gen0={gen0After - gen0Before} last_code={last.Code} last_resp_bytes={last.ResponseBytes.Length}");
    }

    [TestMethod]
    [Ignore("Manual benchmark; not intended for CI.")]
    public void Bench_BridgeDispatcher_MotionAmoApply_AutoSegmentPos()
    {
        const int TotalFrames = 240;

        var sb = new StringBuilder(capacity: 16 * 1024);
        sb.AppendLine("Adobe After Effects 6.0 Keyframe Data");
        sb.AppendLine();
        sb.AppendLine("\tSource Width\t1920");
        sb.AppendLine("\tSource Height\t1080");
        sb.AppendLine();
        sb.AppendLine("Position");
        sb.AppendLine("\tFrame\tX pixels\tY pixels");

        for (int f = 0; f < TotalFrames; f++)
        {
            double x = 960 + 200 * Math.Sin(f * 0.05);
            double y = 540 + 100 * Math.Cos(f * 0.04);
            sb.Append(f).Append('\t').Append(x.ToString("0.###", CultureInfo.InvariantCulture)).Append('\t').Append(y.ToString("0.###", CultureInfo.InvariantCulture)).AppendLine();
        }

        string ae = sb.ToString();

        var call = new MotionAmoApplyCall(
            Context: new BridgeContext(new BridgeScriptResolution(W: 1920, H: 1080)),
            Lines:
            [
                new BridgeLine(
                    Index: 1,
                    Class: "dialogue",
                    TextUtf8: "{\\pos(960,540)}hello"u8.ToArray(),
                    Raw: null,
                    StartTime: 0,
                    EndTime: TotalFrames * 40,
                    StartFrame: 0,
                    EndFrame: TotalFrames,
                    Layer: null,
                    Comment: null,
                    Style: null,
                    Actor: null,
                    Effect: null,
                    MarginL: null,
                    MarginR: null,
                    MarginT: null,
                    Extra: null,
                    Width: null,
                    Height: null,
                    Align: null),
            ],
            Args: new MotionAmoApplyArgs(
                SelectionStartFrame: 0,
                TotalFrames: TotalFrames,
                FrameMs: Enumerable.Range(0, TotalFrames + 1).Select(i => i * 40).ToArray(),
                MainDataUtf8: Encoding.UTF8.GetBytes(ae),
                ClipDataUtf8: null,
                Styles: null,
                Fix: new BridgeAmoFixOptions(Enabled: false, ApplyMain: true, ApplyClip: true, Diff: 0.2, RoundDecimals: 2),
                Main: new BridgeAmoMainOptions(
                    XPosition: true,
                    YPosition: true,
                    Origin: false,
                    AbsPos: false,
                    XScale: false,
                    Border: false,
                    Shadow: false,
                    Blur: false,
                    BlurScale: 1.0,
                    ZRotation: false,
                    ClipOnly: false,
                    RectClip: false,
                    VectClip: false,
                    RcToVc: false,
                    KillTrans: false,
                    Relative: true,
                    StartFrame: 1,
                    LinearMode: BridgeAmoLinearMode.AutoSegmentPos,
                    SegmentPosEps: 0.0,
                    PosErrorMode: BridgeAmoPosErrorMode.Full),
                Clip: new BridgeAmoClipOptions(
                    XPosition: false,
                    YPosition: false,
                    XScale: false,
                    ZRotation: false,
                    RectClip: false,
                    VectClip: false,
                    RcToVc: false,
                    StartFrame: 1)));

        byte[] payload = BridgeMessagePack.SerializeRequest(new BridgeRequest(BridgeMessagePack.SchemaVersion, call));
        byte[] wrapped = BridgeEnvelope.Wrap(payload);

        for (int i = 0; i < 16; i++)
            BridgeDispatcher.Invoke(wrapped);

        long allocBefore = GC.GetAllocatedBytesForCurrentThread();
        int gen0Before = GC.CollectionCount(0);

        const int N = 64;
        var sw = Stopwatch.StartNew();
        BridgeDispatcher.InvokeResult last = default;
        for (int i = 0; i < N; i++)
            last = BridgeDispatcher.Invoke(wrapped);
        sw.Stop();

        long allocAfter = GC.GetAllocatedBytesForCurrentThread();
        int gen0After = GC.CollectionCount(0);

        Console.WriteLine($"Bench_BridgeDispatcher_MotionAmoApply_AutoSegmentPos: frames={TotalFrames} n={N} time_ms={sw.Elapsed.TotalMilliseconds:F2} alloc_bytes={allocAfter - allocBefore} gen0={gen0After - gen0Before} last_code={last.Code} last_resp_bytes={last.ResponseBytes.Length}");
    }
}
