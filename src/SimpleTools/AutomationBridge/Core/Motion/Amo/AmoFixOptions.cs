using Mobsub.AutomationBridge.Ae;
using System.Text;

namespace Mobsub.AutomationBridge.Core.Motion.Amo;

internal readonly record struct AmoFixOptions(
    bool Enabled,
    bool ApplyMain,
    bool ApplyClip,
    double Diff,
    int RoundDecimals)
{
    public static AmoFixOptions Disabled { get; } = new(false, true, true, 0.2, 2);
}

internal static class AmoFixer
{
    private static readonly Encoding Utf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: false);

    public static bool TryApplyFix(
        AmoFixOptions fix,
        ref ReadOnlyMemory<byte> mainDataUtf8,
        ref ReadOnlyMemory<byte> clipDataUtf8,
        bool hasClip,
        List<string> logs,
        out string? error)
    {
        error = null;
        if (!fix.Enabled)
            return true;

        if (fix.ApplyMain && AmoDataParser.LooksLikeAeKeyframeData(mainDataUtf8.Span))
        {
            string mainDataText = Utf8.GetString(mainDataUtf8.Span);
            if (!AeKeyframeDataFixer.TryFixTsr(mainDataText, fix.Diff, fix.RoundDecimals, out var fixedText, out var err))
            {
                error = $"fix(main_data) failed: {err}.";
                return false;
            }
            mainDataUtf8 = Utf8.GetBytes(fixedText);
        }

        if (hasClip && fix.ApplyClip && AmoDataParser.LooksLikeAeKeyframeData(clipDataUtf8.Span))
        {
            string clipDataText = Utf8.GetString(clipDataUtf8.Span);
            if (!AeKeyframeDataFixer.TryFixTsr(clipDataText, fix.Diff, fix.RoundDecimals, out var fixedText, out var err))
            {
                error = $"fix(clip_data) failed: {err}.";
                return false;
            }
            clipDataUtf8 = Utf8.GetBytes(fixedText);
        }

        logs.Add($"fix.enabled: true");
        logs.Add($"fix.diff: {fix.Diff:0.###}");
        logs.Add($"fix.round_decimals: {fix.RoundDecimals}");
        logs.Add($"fix.apply_main: {fix.ApplyMain}");
        logs.Add($"fix.apply_clip: {fix.ApplyClip}");
        return true;
    }
}
