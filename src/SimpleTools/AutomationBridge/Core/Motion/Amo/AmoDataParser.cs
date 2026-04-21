namespace Mobsub.AutomationBridge.Core.Motion.Amo;

internal static class AmoDataParser
{
    private static ReadOnlySpan<byte> Utf8Bom => "\uFEFF"u8;
    private static ReadOnlySpan<byte> TitleAe => "Adobe After Effects 6.0 Keyframe Data"u8;
    private static ReadOnlySpan<byte> TitleShake => "shake_shape_data 4.0"u8;

    public static bool IsNullOrWhiteSpace(ReadOnlySpan<byte> textUtf8)
        => TrimStartWhitespaceAndBom(textUtf8).IsEmpty;

    public static bool LooksLikeAeKeyframeData(ReadOnlySpan<byte> textUtf8)
    {
        var span = TrimStartWhitespaceAndBom(textUtf8);
        return span.StartsWith(TitleAe);
    }

    public static AmoData Parse(ReadOnlySpan<byte> textUtf8, int scriptResX, int scriptResY, int totalFrames, out string? error)
    {
        error = null;
        if (IsNullOrWhiteSpace(textUtf8))
        {
            error = "Empty tracking data.";
            return new AmoNullData();
        }

        var span = TrimStartWhitespaceAndBom(textUtf8);

        if (span.StartsWith(TitleAe))
        {
            var tsr = AmoTsrData.ParseAeTsr(span, scriptResX, scriptResY, out error);
            if (tsr.Length <= 0)
            {
                error ??= "Failed to parse AE Keyframe Data (missing/invalid source width/height or keyframes).";
                return new AmoNullData();
            }
            if (tsr.Length != totalFrames)
            {
                error = $"Tracking length mismatch: expected {totalFrames}, got {tsr.Length}.";
                return new AmoNullData();
            }
            return tsr;
        }

        if (span.StartsWith(TitleShake))
        {
            var srs = AmoSrsData.ParseShakeShape(span, scriptHeight: scriptResY, out error);
            if (srs.Length <= 0)
            {
                error ??= "Failed to parse shake_shape_data.";
                return new AmoNullData();
            }
            if (srs.Length != totalFrames)
            {
                error = $"Tracking length mismatch: expected {totalFrames}, got {srs.Length}.";
                return new AmoNullData();
            }
            return srs;
        }

        error = "Unrecognized motion data format (expected AE Keyframe Data or shake_shape_data 4.0).";
        return new AmoNullData();
    }

    internal static ReadOnlySpan<byte> TrimStartWhitespaceAndBom(ReadOnlySpan<byte> textUtf8)
    {
        int pos = 0;
        while (pos < textUtf8.Length)
        {
            while (pos < textUtf8.Length && IsAsciiWhitespace(textUtf8[pos]))
                pos++;

            if (pos + Utf8Bom.Length <= textUtf8.Length && textUtf8.Slice(pos, Utf8Bom.Length).SequenceEqual(Utf8Bom))
            {
                pos += Utf8Bom.Length;
                continue;
            }

            break;
        }

        return pos == 0 ? textUtf8 : textUtf8[pos..];
    }

    private static bool IsAsciiWhitespace(byte b)
        => b is (byte)' ' or (byte)'\t' or (byte)'\r' or (byte)'\n';
}
