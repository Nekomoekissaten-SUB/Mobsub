using System.Buffers;
using System.Text;
using Mobsub.SubtitleParseNT2.AssUtils;

namespace Mobsub.SubtitleParseNT2.AssTypes;

public struct AssEvent
{
    public int LineNumber { get; set; }
    public ReadOnlyMemory<byte> LineRaw { get; set; }

    public bool StartSemicolon { get; set; }
    public bool IsDialogue { get; set; }
    public int Layer { get; set; }
    public int Marked { get; set; }
    public AssTime Start { get; set; }
    public AssTime End { get; set; }
    public Range StyleReadOnly { get; set; }
    public Range NameReadOnly { get; set; }
    public int MarginL { get; set; }
    public int MarginR { get; set; }
    public int MarginV { get; set; }
    public int MarginT { get; set; }
    public int MarginB { get; set; }
    public Range EffectReadOnly { get; set; }
    public Range TextReadOnly { get; set; }

    // Hybrid Editing Fields
    private string? _style;
    private string? _name;
    private string? _effect;
    private string? _text;

    public string Style
    {
        get => _style ?? Utils.GetString(LineRaw, StyleReadOnly);
        set => _style = value;
    }

    public string Name
    {
        get => _name ?? Utils.GetString(LineRaw, NameReadOnly);
        set => _name = value;
    }

    public string Effect
    {
        get => _effect ?? Utils.GetString(LineRaw, EffectReadOnly);
        set => _effect = value;
    }

    public string Text
    {
        get => _text ?? Utils.GetString(LineRaw, TextReadOnly);
        set => _text = value;
    }

    // Zero-copy accessors (safe to use if not modified)
    public ReadOnlySpan<byte> StyleSpan => LineRaw.Span[StyleReadOnly];
    public ReadOnlySpan<byte> NameSpan => LineRaw.Span[NameReadOnly];
    public ReadOnlySpan<byte> EffectSpan => LineRaw.Span[EffectReadOnly];
    public ReadOnlySpan<byte> TextSpan => LineRaw.Span[TextReadOnly];

    public AssEvent(ReadOnlyMemory<byte> line, int lineNum, ReadOnlySpan<byte> header, string[] formats)
    {
        LineNumber = lineNum;
        LineRaw = line;

        StartSemicolon = false;
        IsDialogue = true;
        Layer = 0;
        Marked = 0;
        Start = default;
        End = default;

        StyleReadOnly = default;
        NameReadOnly = default;
        MarginL = 0;
        MarginR = 0;
        MarginV = 0;
        MarginT = 0;
        MarginB = 0;
        EffectReadOnly = default;
        TextReadOnly = default;

        _style = null;
        _name = null;
        _effect = null;
        _text = null;

        var sp = line.Span;
        if (header.SequenceEqual(";"u8))
        {
            StartSemicolon = true;
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

        var sepIndex = header.Length;
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
                case "Style": StyleReadOnly = new Range(sepIndex, nextSep); break;
                case "Name": NameReadOnly = new Range(sepIndex, nextSep); break;
                case "MarginL": MarginL = int.Parse(value); break;
                case "MarginR": MarginR = int.Parse(value); break;
                case "MarginV": MarginV = int.Parse(value); break;
                case "MarginT": MarginT = int.Parse(value); break;
                case "MarginB": MarginB = int.Parse(value); break;
                case "Effect": EffectReadOnly = new Range(sepIndex, nextSep); break;
            }

            segCount++;
            sepIndex = nextSep + 1;
        }
        TextReadOnly = Range.StartAt(sepIndex);
    }

    public Range[] TextRanges { get; set; } = [];

    public void UpdateTextRanges()
    {
        var text = Text; // Use property to ensure we get string
        if (string.IsNullOrEmpty(text))
        {
            TextRanges = [];
            return;
        }

        var list = new List<Range>();
        var s = text.AsSpan();
        int i = 0;
        while (i < s.Length)
        {
            if (s[i] == '{')
            {
                int k = s.Slice(i + 1).IndexOf('}');
                if (k != -1)
                {
                    // Found block
                    int len = k + 2; // +1 for offset, +1 for }
                    list.Add(new Range(i, i + len));
                    i += len;
                    continue;
                }
            }

            // Text content
            int j = i + 1;
            while (j < s.Length && s[j] != '{') j++;
            list.Add(new Range(i, j));
            i = j;
        }
        TextRanges = [.. list];
    }

    public static bool IsOverrideBlock(ReadOnlySpan<char> s) => s.Length >= 2 && s[0] == '{' && s[^1] == '}';
}
