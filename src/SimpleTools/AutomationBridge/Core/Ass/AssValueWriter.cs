using System.Globalization;
using System.Text;

namespace Mobsub.AutomationBridge.Core.Ass;

public static class AssValueWriter
{
    private const string Format0 = "0";
    private const string Format1 = "0.#";
    private const string Format2 = "0.##";
    private const string Format3 = "0.###";

    public static void AppendInt(StringBuilder sb, int value)
    {
        Span<char> buffer = stackalloc char[16];
        if (!value.TryFormat(buffer, out int written, provider: CultureInfo.InvariantCulture))
        {
            sb.Append(value.ToString(CultureInfo.InvariantCulture));
            return;
        }
        sb.Append(buffer.Slice(0, written));
    }

    public static void AppendNumber(StringBuilder sb, double value, int maxDecimals)
    {
        value = Math.Round(value, maxDecimals, MidpointRounding.AwayFromZero);
        ReadOnlySpan<char> format = maxDecimals switch
        {
            0 => Format0,
            1 => Format1,
            2 => Format2,
            3 => Format3,
            _ => Format3
        };

        Span<char> buffer = stackalloc char[32];
        if (!value.TryFormat(buffer, out int written, format, CultureInfo.InvariantCulture))
        {
            sb.Append(value.ToString(format.ToString(), CultureInfo.InvariantCulture));
            return;
        }
        sb.Append(buffer.Slice(0, written));
    }
}
