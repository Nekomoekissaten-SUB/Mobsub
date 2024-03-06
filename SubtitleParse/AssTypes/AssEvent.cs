using System.Diagnostics;
using System.Text;
using static Mobsub.Utils.ParseHelper;

namespace Mobsub.AssTypes;

public class AssEvents
{
    private readonly string formatV4P = "Layer, Start, End, Style, Name, MarginL, MarginR, MarginV, Effect, Text";
    // private readonly string formatV4PP = "Layer, Start, End, Style, Name, MarginL, MarginR, MarginT, MarginB, Effect, Text";
    // private readonly string formatV4 = "Marked, Start, End, Style, Name, MarginL, MarginR, MarginV, Effect, Text";
    private string[]? formats;
    public string[] Formats
    {
        get => formats ?? formatV4P.Split(',').Select(s => s.Trim()).ToArray();
        set => formats = value;
    }
    public List<AssEvent> Collection = [];

    public void Read(ReadOnlySpan<char> sp, string scriptType, int lineNumber)
    {
        var sepIndex = sp.IndexOf(':');

        if (sp[0] == ';' || sepIndex < 1)
        {
            Collection.Add(new AssEvent() { StartSemicolon = true, Untouched = sp.ToString(), lineNumber = lineNumber });
            return;
        }

        if (sp[..sepIndex].SequenceEqual("Format".AsSpan()))
        {
            if (scriptType.AsSpan().SequenceEqual("v4.00++".AsSpan()))
            {
                throw new Exception($"{scriptType} not have format line");
            }
            Formats = sp[(sepIndex + 1)..].ToString().Split(',').Select(s => s.Trim()).ToArray();
            if (!Formats[^1].AsSpan().SequenceEqual("Text".AsSpan()))
            {
                throw new Exception("Events: Text must be last field.");
            }
        }
        else if (sp[..sepIndex].SequenceEqual("Dialogue".AsSpan()) || sp[..sepIndex].SequenceEqual("Comment".AsSpan()))
        {
            var evt = new AssEvent() { IsDialogue = sp[..sepIndex].SequenceEqual("Dialogue".AsSpan()), lineNumber = lineNumber };
            sepIndex += (char.IsWhiteSpace(sp[sepIndex + 1])) ? 2 : 1;
            evt.ReadWithoutHeader(sp[sepIndex..], Formats);
            Collection.Add(evt);
        }
        else
        {
            throw new Exception($"Unknown Events line {sp.ToString()}");
        }
    }

    public void Write(StreamWriter sw, char[] newline, bool ctsRounding)
    {
        sw.Write("[Events]");
        sw.Write(newline);
        sw.Write($"Format: {string.Join(", ", Formats)}");
        sw.Write(newline);

        for (var i = 0; i < Collection.Count; i++)
        {
            Collection[i].Write(sw, Formats, ctsRounding);
            sw.Write(newline);
        }
    }
}

public class AssEvent
{
    private int layer = 0;
    public int lineNumber;
    public bool StartSemicolon = false;
    public string? Untouched = string.Empty;
    public bool IsDialogue = true;
    public int Layer
    {
        get => layer;
        set => layer = value >= 0 ? value : 0;
    }
    public readonly int Marked = 0;
    public AssTime Start { get; set; }
    public AssTime End { get; set; }
    public string Style { get; set; } = "Default";
    public string Name { get; set; } = string.Empty;
    public int MarginL { get; set; } = 0;
    public int MarginR { get; set; } = 0;
    public int MarginV { get; set; } = 0;
    public int MarginT { get; set; } = 0;
    public int MarginB { get; set; } = 0;
    public string? Effect { get; set; }
    public List<char[]> Text { get; set; } = [];  // override tags block, special chars block, normal text block

    public void ReadWithoutHeader(ReadOnlySpan<char> sp, string[] fmts)
    {
        var startIndex = 0;
        var nextSep = 0;
        var sepChar = ',';
        var segCount = 0;

        while (segCount < fmts.Length - 1)
        {
            nextSep = sp[startIndex..].IndexOf(sepChar) + startIndex;

            var v = sp[startIndex..nextSep].ToString();

            switch (fmts[segCount])
            {
                case "Marked":
                    break;
                default:
                    SetProperty(this, fmts[segCount], v);
                    break;
            }

            segCount++;
            startIndex = nextSep + 1;
        }

        Text = ParseEventText2(sp[startIndex..]);
    }

    private static List<char[]> ParseEventText2(ReadOnlySpan<char> span)
    {
        var records = new List<char[]>();
        var sb = new StringBuilder();
        var blk = false;
        var backslash = false;
        var _start = 0;
        for (var i = 0; i < span.Length; i++)
        {
            var c = span[i];
            switch (c)
            {
                case AssConstants.StartOvrBlock:
                    if (!blk)
                    {
                        EventAddRecord(span, ref _start, i, ref records);
                        _start = i;
                        blk = true;
                    }
                    break;
                case AssConstants.EndOvrBlock:
                    if (blk)
                    {
                        EventAddRecord(span, ref _start, i + 1, ref records);
                        blk = false;
                    }
                    break;
                case AssConstants.BackSlash:
                    if (!blk)
                    {
                        EventAddRecord(span, ref _start, i, ref records);
                        _start = i;
                        backslash = true;
                    }
                    break;
                case AssConstants.NBSP:
                case AssConstants.WordBreaker:
                case AssConstants.LineBreaker:
                    if (backslash)
                    {
                        EventAddRecord(span, ref _start, i + 1, ref records);
                        backslash = false;
                    }
                    break;
                default:
                    backslash = false;
                    break;
            }
        }

        if (_start < span.Length)
        {
            EventAddRecord(span, ref _start, span.Length, ref records);
        }
        Debug.Assert(records.Sum(l => l.Length) == span.Length, $"Parse records length is {records.Sum(l => l.Length)}, should be {span.Length}");
        return records;
    }
    private static void EventAddRecord(ReadOnlySpan<char> span, ref int _start, int end, ref List<char[]> records)
    {
        if (_start < end)
        {
            var length = end - _start;
            var record = new char[length];
            span[_start..end].CopyTo(record);
            records.Add(record);
        }
        _start = end;
    }

    public static void WriteTime(StreamWriter sw, AssTime time, bool ctsRounding)
    {
        sw.Write(time.Hour);
        sw.Write(':');
        WriteChar(sw, time.Minute, 2);
        sw.Write(':');
        WriteChar(sw, time.Second, 2);
        sw.Write('.');

        if (ctsRounding)
        {
            WriteChar(sw, DigitRounding(time.Millisecond), 3);
        }
        else
        {
            WriteChar(sw, time.Millisecond, 3);
        }
        
    }

    private static void WriteChar(StreamWriter sw, int val, int length)
    {
        var ca = new char[length];
        
        var divisor = 1;
        for (var i = 1; i < length; i++)
        {
            divisor *= 10;
        }

        for (int i = 0; i < length; i++)
        {
            ca[i] = (char)(val / divisor + '0');
            val %= divisor;
            divisor /= 10;
        }

        sw.Write(ca[0..Math.Min(length, 2)]);
    }

    private static int DigitRounding(int i)
    {
        var last = i % 10;
        return (i > 994) ? 990 : last >= 0 && last <= 4 ? i - last : i + (10 - last);
    }

    public void Write(StreamWriter sw, string[] fmts, bool ctsRounding)
    {
        if (StartSemicolon)
        {
            sw.Write($";{Untouched}");
        }
        else
        {
            sw.Write(IsDialogue ? "Dialogue: " : "Comment: ");
            
            for (var i = 0; i < fmts.Length; i++)
            {
                // var fmt = fmts[i];
                switch (fmts[i])
                {
                    case "Marked":
                        sw.Write(Marked);
                        break;
                    case "Layer":
                        sw.Write(Layer);
                        break;
                    case "Start":
                        WriteTime(sw, Start, ctsRounding);
                        break;
                    case "End":
                        WriteTime(sw, End, ctsRounding);
                        break;
                    case "Style":
                        sw.Write(Style);
                        break;
                    case "Name":
                        sw.Write(Name);
                        break;
                    case "MarginL":
                        sw.Write(MarginL);
                        break;
                    case "MarginR":
                        sw.Write(MarginR);
                        break;
                    case "MarginV":
                        sw.Write(MarginV);
                        break;
                    case "MarginT":
                        sw.Write(MarginT);
                        break;
                    case "MarginB":
                        sw.Write(MarginB);
                        break;
                    case "Effect":
                        sw.Write(Effect);
                        break;
                    case "Text":
                        foreach (var ca in Text)
                        {
                            sw.Write(ca);
                        }
                        break;
                }
                
                if (i < fmts.Length - 1)
                {
                    sw.Write(',');
                }
            }
        }
    }

}
