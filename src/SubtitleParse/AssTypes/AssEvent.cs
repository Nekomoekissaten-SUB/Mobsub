using System.Buffers;
using System.Buffers.Text;
using System.Text;
using Mobsub.SubtitleParse.AssUtils;

namespace Mobsub.SubtitleParse.AssTypes;

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

    public uint[]? AegisubExtradataIds { get; set; }

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

    public int lineNumber
    {
        get => LineNumber;
        set => LineNumber = value;
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
        AegisubExtradataIds = null;

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
                case "Layer": Layer = Utils.ParseInt(value); break;
                case "Marked": break;
                case "Start": Start = AssTime.ParseFromAss(value); break;
                case "End": End = AssTime.ParseFromAss(value); break;
                case "Style": StyleReadOnly = new Range(sepIndex, nextSep); break;
                case "Name": NameReadOnly = new Range(sepIndex, nextSep); break;
                case "MarginL": MarginL = Utils.ParseInt(value); break;
                case "MarginR": MarginR = Utils.ParseInt(value); break;
                case "MarginV": MarginV = Utils.ParseInt(value); break;
                case "MarginT": MarginT = Utils.ParseInt(value); break;
                case "MarginB": MarginB = Utils.ParseInt(value); break;
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

    public AssEvent DeepClone() => this;

    public bool TryExtractAegisubExtradataMarkerFromText()
    {
        var textSpan = TextSpan;
        if (!TryParseAegisubExtradataMarker(textSpan, out var ids, out int markerLen))
            return false;

        AegisubExtradataIds = ids;
        _text = Utils.GetString(textSpan[markerLen..]);
        return true;
    }

    private static bool TryParseAegisubExtradataMarker(ReadOnlySpan<byte> text, out uint[] ids, out int markerLengthBytes)
    {
        ids = Array.Empty<uint>();
        markerLengthBytes = 0;

        if (text.Length < 4 || text[0] != (byte)'{' || text[1] != (byte)'=')
            return false;

        var list = new List<uint>();

        int pos = 1;
        while (pos < text.Length && text[pos] == (byte)'=')
        {
            pos++;
            int start = pos;
            while (pos < text.Length && text[pos] >= (byte)'0' && text[pos] <= (byte)'9')
                pos++;
            if (pos == start)
                return false;

            if (!Utf8Parser.TryParse(text.Slice(start, pos - start), out uint id, out int consumed) || consumed != (pos - start))
                return false;

            list.Add(id);

            if (pos >= text.Length)
                return false;

            if (text[pos] == (byte)'}')
            {
                markerLengthBytes = pos + 1;
                ids = list.Count > 0 ? list.ToArray() : Array.Empty<uint>();
                return true;
            }

            if (text[pos] != (byte)'=')
                return false;
        }

        return false;
    }
}
