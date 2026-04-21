using System.IO;
using Mobsub.SubtitleParse.AssTypes;

namespace Mobsub.SubtitleParse.AssUtils;

public static class AssFormatLineWriter
{
    private const string CommaSpace = ", ";

    public static void WriteFormatLine(TextWriter writer, string[] formats, string newline)
    {
        writer.Write(AssConstants.FormatLinePrefix);
        WriteCommaSeparated(writer, formats);
        writer.Write(newline);
    }

    public static void WriteFormatLine(TextWriter writer, IReadOnlyList<string> formats, string newline)
    {
        writer.Write(AssConstants.FormatLinePrefix);
        WriteCommaSeparated(writer, formats);
        writer.Write(newline);
    }

    public static void WriteFormatLine(TextWriter writer, string[] formats, ReadOnlySpan<char> newline)
    {
        writer.Write(AssConstants.FormatLinePrefix);
        WriteCommaSeparated(writer, formats);
        writer.Write(newline);
    }

    public static void WriteFormatLine(TextWriter writer, IReadOnlyList<string> formats, ReadOnlySpan<char> newline)
    {
        writer.Write(AssConstants.FormatLinePrefix);
        WriteCommaSeparated(writer, formats);
        writer.Write(newline);
    }

    public static void WriteCommaSeparated(TextWriter writer, string[] values)
    {
        for (int i = 0; i < values.Length; i++)
        {
            if (i > 0)
                writer.Write(CommaSpace);
            writer.Write(values[i]);
        }
    }

    public static void WriteCommaSeparated(TextWriter writer, IReadOnlyList<string> values)
    {
        for (int i = 0; i < values.Count; i++)
        {
            if (i > 0)
                writer.Write(CommaSpace);
            writer.Write(values[i]);
        }
    }
}
