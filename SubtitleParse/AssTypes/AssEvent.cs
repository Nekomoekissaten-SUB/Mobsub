using Microsoft.Extensions.Logging;
using ZLogger;
using System.Diagnostics;
using System.Text;

namespace Mobsub.SubtitleParse.AssTypes;

public class AssEvents(ILogger<AssData>? logger = null)
{
    private string[]? formats;
    public string[] Formats
    {
        get => formats ?? AssConstants.FormatV4P.Split(',').Select(s => s.Trim()).ToArray();
        set => formats = value;
    }
    public List<AssEvent> Collection = [];
    private readonly ILogger<AssData>? _logger = logger;
    internal const string sectionName = "[Events]";

    public void Read(ReadOnlySpan<char> sp, string scriptType, int lineNumber)
    {
        var sepIndex = sp.IndexOf(':');
        var evt = new AssEvent(_logger);
        if (evt.Read(sp, sepIndex, lineNumber, Formats))
        {
            Collection.Add(evt);
        }
        else
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
            _logger?.ZLogDebug($"Parse format line fine");
        }
    }

    public void Write(StreamWriter sw, char[] newline, bool ctsRounding)
    {
        _logger?.ZLogInformation($"Start write section {sectionName}");
        sw.Write(sectionName);
        sw.Write(newline);
        sw.Write($"Format: {string.Join(", ", Formats)}");
        _logger?.ZLogDebug($"Write format line fine");
        sw.Write(newline);

        _logger?.ZLogDebug($"Start Write event line");
        for (var i = 0; i < Collection.Count; i++)
        {
            Collection[i].Write(sw, Formats, ctsRounding);
            sw.Write(newline);
        }
        _logger?.ZLogDebug($"Write event lines fine");
    }
}

public class AssEvent(ILogger<AssData>? logger = null)
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
    private readonly ILogger<AssData>? _logger = logger;

    public bool Read(ReadOnlySpan<char> sp, int lineNum, string[] formats) => Read(sp, sp.IndexOf(':'), lineNum, formats);

    public bool Read(ReadOnlySpan<char> sp, int sepIndex, int lineNum, string[] formats)
    {
        if (sp[0] == ';' || sepIndex < 1)
        {
            StartSemicolon = true;
            Untouched = sp.ToString();
            lineNumber = lineNum;
            _logger?.ZLogInformation($"Line ${lineNum} is a comment line, will record untouched");
            return true;
        }

        var header = sp[..sepIndex];
        if (header.SequenceEqual("Format".AsSpan()))
        {
            return false;
        }
        else if (header.SequenceEqual("Dialogue".AsSpan()) || header.SequenceEqual("Comment".AsSpan()))
        {
            IsDialogue = header.SequenceEqual("Dialogue".AsSpan());
            lineNumber = lineNum;
            sepIndex += (char.IsWhiteSpace(sp[sepIndex + 1])) ? 2 : 1;
            ReadWithoutHeader(sp[sepIndex..], formats);
            return true;
        }
        else
        {
            throw new Exception($"Unknown Events line {sp.ToString()}");
        }
    }

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
                    Utils.SetProperty(this, typeof(AssEvent), fmts[segCount], v);
                    break;
            }

            segCount++;
            startIndex = nextSep + 1;
        }

        Text = ParseEventText(sp[startIndex..]);
    }

    public static List<char[]> ParseEventText(ReadOnlySpan<char> span)
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

    public static void WriteTime(StringBuilder sb, AssTime time, bool ctsRounding)
    {
        sb.Append(time.Hour);
        sb.Append(':');
        WriteChar(sb, time.Minute, 2);
        sb.Append(':');
        WriteChar(sb, time.Second, 2);
        sb.Append('.');

        if (ctsRounding)
        {
            WriteChar(sb, DigitRounding(time.Millisecond), 3);
        }
        else
        {
            WriteChar(sb, time.Millisecond, 3);
        }
        
    }

    private static void WriteChar(StringBuilder sb, int val, int length)
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

        sb.Append(ca[0..Math.Min(length, 2)]);
    }

    private static int DigitRounding(int i)
    {
        var last = i % 10;
        return (i > 994) ? 990 : last >= 0 && last <= 4 ? i - last : i + (10 - last);
    }

    public void Write(StreamWriter sw, string[] fmts, bool ctsRounding)
    {
        var sb = new StringBuilder();
        Write(sb, fmts, ctsRounding);
        sw.Write(sb.ToString());
    }

    public void Write(StringBuilder sb, string[] fmts, bool ctsRounding)
    {
        if (StartSemicolon)
        {
            sb.Append($";{Untouched}");
        }
        else
        {
            sb.Append(IsDialogue ? "Dialogue: " : "Comment: ");
            
            for (var i = 0; i < fmts.Length; i++)
            {
                // var fmt = fmts[i];
                switch (fmts[i])
                {
                    case "Marked":
                        sb.Append(Marked);
                        break;
                    case "Layer":
                        sb.Append(Layer);
                        break;
                    case "Start":
                        WriteTime(sb, Start, ctsRounding);
                        break;
                    case "End":
                        WriteTime(sb, End, ctsRounding);
                        break;
                    case "Style":
                        sb.Append(Style);
                        break;
                    case "Name":
                        sb.Append(Name);
                        break;
                    case "MarginL":
                        sb.Append(MarginL);
                        break;
                    case "MarginR":
                        sb.Append(MarginR);
                        break;
                    case "MarginV":
                        sb.Append(MarginV);
                        break;
                    case "MarginT":
                        sb.Append(MarginT);
                        break;
                    case "MarginB":
                        sb.Append(MarginB);
                        break;
                    case "Effect":
                        sb.Append(Effect);
                        break;
                    case "Text":
                        foreach (var ca in Text)
                        {
                            sb.Append(ca);
                        }
                        break;
                }
                
                if (i < fmts.Length - 1)
                {
                    sb.Append(',');
                }
            }
        }
    }

}
