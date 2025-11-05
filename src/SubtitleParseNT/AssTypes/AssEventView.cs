using Microsoft.Extensions.Logging;
using System.Text;
using ZLogger;

namespace Mobsub.SubtitleParseNT2.AssTypes;

public sealed class AssEventView
{
    public readonly int LineNumber;
    public readonly ReadOnlyMemory<byte> LineRaw;

    public readonly bool StartSemicolon = false;
    public readonly bool IsDialogue = true;
    public int Layer { get; init; }
    public int Marked { get; init; } = 0;
    public AssTime Start { get; init; }
    public AssTime End { get; init; }
    public Range Style { get; init; }
    public Range Name { get; init; }
    public int MarginL { get; init; }
    public int MarginR { get; init; }
    public int MarginV { get; init; }
    public int MarginT { get; init; }
    public int MarginB { get; init; }
    public Range Effect { get; init; }
    public Range Text { get; init; }

    public string GetStyle() => Utils.GetString(LineRaw, Style);
    public string GetName() => Utils.GetString(LineRaw, Name);
    public string GetEffect() => Utils.GetString(LineRaw, Effect);
    public string GetText() => Utils.GetString(LineRaw, Text);

    public void Write(StringBuilder sb)
    {
        sb.AppendLine(Utils.GetString(LineRaw));
    }

    public AssEventView(ReadOnlyMemory<byte> line, int lineNum, ReadOnlySpan<byte> header, string[] formats, ILogger? logger = null)
    {
        var sp = line.Span;
        LineRaw = line;
        LineNumber = lineNum;

        var sepIndex = header.Length;

        if (header.SequenceEqual(";"u8))
        {
            StartSemicolon = true;
            logger?.ZLogInformation($"Line ${lineNum} is a comment line, will pass parse");
            return;
        }
        else if (header.SequenceEqual("Format"u8))
        {
            return;
        }
        else if (header.SequenceEqual("Dialogue"u8))
        {
            IsDialogue = true;
        }
        else if (header.SequenceEqual("Comment"u8))
        {
            IsDialogue = false;
        }
        else
        {
            throw new Exception($"Unknown Events line '{Utils.GetString(line)}'");
        }

        sepIndex += (sp[sepIndex + 1] == 0x20) ? 2 : 1;
        var segCount = 0;
        int nextSep;

        while (segCount < formats.Length - 1)
        {
            nextSep = sp[sepIndex..].IndexOf(((byte)','));
            if (nextSep == -1) throw new FormatException($"Invalid line: '{Utils.GetString(line)}'");
            nextSep += sepIndex;
            var value = sp[sepIndex..nextSep];
            switch (formats[segCount])
            {
                case "Layer": Layer = int.Parse(value); break;
                case "Marked": break;
                case "Start": Start = AssTime.ParseFromAss(value); break;
                case "End": End = AssTime.ParseFromAss(value); break;
                case "Style": Style = new Range(sepIndex, nextSep); break;
                case "Name": Name = new Range(sepIndex, nextSep); break;
                case "MarginL": MarginL = int.Parse(value); break;
                case "MarginR": MarginR = int.Parse(value); break;
                case "MarginV": MarginV = int.Parse(value); break;
                case "MarginT": MarginT = int.Parse(value); break;
                case "MarginB": MarginB = int.Parse(value); break;
                case "Effect": Effect = new Range(sepIndex, nextSep); break;
            }

            segCount++;
            sepIndex = nextSep + 1;
        }
        Text = Range.StartAt(sepIndex);
    }
}
