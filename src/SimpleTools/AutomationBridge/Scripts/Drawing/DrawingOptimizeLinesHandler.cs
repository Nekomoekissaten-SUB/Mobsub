using System.Text;
using Mobsub.AutomationBridge.Core.Ass;
using Mobsub.AutomationBridge.Dispatch;
using Mobsub.AutomationBridge.Protocol;
using Mobsub.SubtitleParse.AssText;

namespace Mobsub.AutomationBridge.Scripts.Drawing;

internal static class DrawingOptimizeLinesHandler
{
    private const string ClassDialogue = "dialogue";

    public static BridgeHandlerResult Handle(DrawingOptimizeLinesCall call, List<string> logs)
    {
        var lines = call.Lines;
        if (lines is null || lines.Length == 0)
            return BadArgs("lines is required and must be non-empty.", logs);

        double curveTol = call.Args.CurveTolerance;
        double simplifyTol = call.Args.SimplifyTolerance;
        int precision = call.Args.PrecisionDecimals;

        int changed = 0;
        var ops = new List<IBridgePatchOp>(lines.Length);

        foreach (var line in lines)
        {
            if (!string.Equals(line.Class, ClassDialogue, StringComparison.OrdinalIgnoreCase))
                continue;

            if (line.TextUtf8 is not { } textUtf8 || textUtf8.Length == 0)
                continue;

            using var read = AssEventTextRead.Parse(textUtf8);
            if (!AssSubtitleParseTagEditor.TryGetPolygonMode(read, out int p, out int blockEnd))
                continue;

            if (p <= 0 || blockEnd <= 0)
                continue;

            ReadOnlySpan<byte> lineUtf8 = read.Utf8.Span;
            ReadOnlySpan<byte> drawingUtf8 = lineUtf8.Slice(blockEnd);
            string drawing = Encoding.UTF8.GetString(drawingUtf8).Trim();
            if (drawing.Length == 0)
                continue;

            string optimized;
            try
            {
                optimized = AssDrawingOptimizer.OptimizeDrawing(drawing, curveTol, simplifyTol, precisionDecimals: precision, closeContours: false);
            }
            catch (Exception ex)
            {
                logs.Add($"optimize_failed line={line.Index}: {ex.Message}");
                continue;
            }

            if (string.IsNullOrEmpty(optimized) || string.Equals(optimized, drawing, StringComparison.Ordinal))
                continue;

            ReadOnlySpan<byte> prefixUtf8 = lineUtf8.Slice(0, blockEnd);
            byte[] optimizedUtf8 = Encoding.UTF8.GetBytes(optimized);

            var newTextUtf8 = new byte[prefixUtf8.Length + optimizedUtf8.Length];
            prefixUtf8.CopyTo(newTextUtf8);
            optimizedUtf8.CopyTo(newTextUtf8.AsSpan(prefixUtf8.Length));

            if (newTextUtf8.AsSpan().SequenceEqual(lineUtf8))
                continue;

            ops.Add(new BridgeSetTextPatchOp(
                Index: line.Index,
                TextUtf8: newTextUtf8));
            changed++;
        }

        logs.Add($"drawing_optimized: {changed}");
        BridgePatch? patch = ops.Count > 0 ? new BridgePatch(ops.ToArray()) : null;
        var resp = new BridgeResponse(true, null, logs.ToArray(), patch, Result: null, Methods: null);
        return new BridgeHandlerResult(BridgeErrorCodes.Ok, resp);
    }

    private static BridgeHandlerResult BadArgs(string message, List<string> logs)
        => new(BridgeErrorCodes.ErrBadArgs, new BridgeResponse(false, message, logs.ToArray(), Patch: null, Result: null, Methods: null));
}
