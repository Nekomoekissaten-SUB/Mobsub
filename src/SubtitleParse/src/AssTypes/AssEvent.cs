using Microsoft.Extensions.Logging;
using ZLogger;
using System.Diagnostics;
using System.Text;

namespace Mobsub.SubtitleParse.AssTypes;

public class AssEvents(ILogger? logger = null)
{
    private string[]? formats;
    public string[] Formats
    {
        get => formats ?? AssConstants.FormatV4P.Split(',').Select(s => s.Trim()).ToArray();
        set => formats = value;
    }
    public List<AssEvent> Collection = [];
    internal const string sectionName = "[Events]";

    public void Read(ReadOnlySpan<char> sp, string scriptType, int lineNumber)
    {
        var sepIndex = sp.IndexOf(':');
        var evt = new AssEvent(logger);
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
            logger?.ZLogDebug($"Parse format line fine");
        }
    }

    public void Write(StreamWriter sw, char[] newline, bool ctsRounding)
    {
        logger?.ZLogInformation($"Start write section {sectionName}");
        sw.Write(sectionName);
        sw.Write(newline);
        sw.Write($"Format: {string.Join(", ", Formats)}");
        sw.Write(newline);
        logger?.ZLogDebug($"Write format line fine");

        logger?.ZLogDebug($"Start Write event line");
        for (var i = 0; i < Collection.Count; i++)
        {
            Collection[i].Write(sw, Formats, ctsRounding);
            sw.Write(newline);
        }
        //sw.Write(newline);
        logger?.ZLogDebug($"Write event lines fine");
    }
}

public partial class AssEvent(ILogger? logger = null)
{
    public int lineNumber;
    public bool StartSemicolon = false;
    public string? Untouched = string.Empty;
    [AssProperty] private bool _isDialogue = true;
    [AssProperty] private int _layer;
    public readonly int Marked = 0;
    [AssProperty] private AssTime _start;
    [AssProperty] private AssTime _end;
    [AssProperty] private string _style = "Default";
    [AssProperty] private string _name = string.Empty;
    [AssProperty] private int _marginL;
    [AssProperty] private int _marginR;
    [AssProperty] private int _marginV;
    [AssProperty] private int _marginT;
    [AssProperty] private int _marginB;
    [AssProperty] private string? _effect;
    [AssProperty(InvalidatesProperties = new[] { nameof(TextRanges) })]
    private string? _text;

    [AssCachedProperty(CalculationMethod = nameof(CalculateTextRanges))]
    private Range[] TextRangesCache = [];
    private Range[] CalculateTextRanges() => SplitEventText(_text.AsSpan());

    public bool Read(ReadOnlySpan<char> sp, int lineNum, string[] formats) => Read(sp, sp.IndexOf(':'), lineNum, formats);

    public bool Read(ReadOnlySpan<char> sp, int sepIndex, int lineNum, string[] formats)
    {
        if (sp[0] == ';' || sepIndex < 1)
        {
            StartSemicolon = true;
            Untouched = sp.ToString();
            lineNumber = lineNum;
            logger?.ZLogInformation($"Line ${lineNum} is a comment line, will record untouched");
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

            if (nextSep < startIndex)
            {
                throw new FormatException($"Invalid line: '{sp.ToString()}'");
            }
            
            var v = sp[startIndex..nextSep].TrimStart();

            switch (fmts[segCount])
            {
                case "Marked":
                    break;
                case "Style":
                    var target = GetEventStyleName(v);
                    if (!target.SequenceEqual(v))
                    {
                        logger?.ZLogWarning($"The style of line {lineNumber} will be fixed from '{v.ToString()}' to '{target.ToString()}'.");
                    }
                    Style = target.ToString();
                    break;
                default:
                    Utils.SetProperty(this, typeof(AssEvent), fmts[segCount], v);
                    break;
            }

            segCount++;
            startIndex = nextSep + 1;
        }

        Text = sp[startIndex..].ToString();
    }

    /// <summary>
    /// Split to override tags block, special chars block and normal text block
    /// </summary>
    /// <param name="span"></param>
    /// <returns></returns>
    public static Range[] SplitEventText(ReadOnlySpan<char> span)
    {
        List<Range> ranges = [];

        var inBlock = false;
        var backslash = false;
        var start = 0;
        for (var i = 0; i < span.Length; i++)
        {
            switch (span[i])
            {
                case AssConstants.StartOvrBlock:
                    if (!inBlock)
                    {
                        if (start != i)
                        {
                            ranges.Add(new Range(start, i));
                            start = i;
                        }
                        inBlock = true;
                    }
                    break;
                case AssConstants.EndOvrBlock:
                    if (inBlock)
                    {
                        ranges.Add(new Range(start, i + 1));
                        start = i + 1;
                        inBlock = false;
                    }
                    break;
                case AssConstants.BackSlash:
                    if (!inBlock)
                    {
                        if (start != i)
                        {
                            ranges.Add(new Range(start, i));
                            start = i;
                        }
                        backslash = true;
                    }
                    break;
                case AssConstants.NoBreakSpace:
                case AssConstants.WordBreaker:
                case AssConstants.LineBreaker:
                    if (backslash)
                    {
                        ranges.Add(new Range(start, i + 1));
                        start = i + 1;
                        backslash = false;
                    }
                    break;
                default:
                    backslash = false;
                    break;
            }
        }

        if (start < span.Length)
        {
            ranges.Add(new Range(start, span.Length));
        }
        
        return ranges.ToArray();
    }
    public void UpdateTextRanges() => InvalidateTextRanges();

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
                        AssTime.WriteAssTime(sb, Start, ctsRounding);
                        break;
                    case "End":
                        AssTime.WriteAssTime(sb, End, ctsRounding);
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
                        sb.Append(Text);
                        break;
                }
                
                if (i < fmts.Length - 1)
                {
                    sb.Append(',');
                }
            }
        }
    }

    // Utils
    public static bool IsOverrideBlock(ReadOnlySpan<char> block)
    {
        if (block.Length < 2)
        {
            return false;
        }
        return block[0] == AssConstants.StartOvrBlock && block[^1] == AssConstants.EndOvrBlock;
    }
    public static bool IsEventSpecialCharPair(ReadOnlySpan<char> ca) =>
        ca.Length == 2 && ca[0] == '\\' &&
        ca[1] is AssConstants.LineBreaker or AssConstants.WordBreaker or AssConstants.NoBreakSpace;
    public static bool IsTextBlock(ReadOnlySpan<char> block) => !(IsOverrideBlock(block) || IsEventSpecialCharPair(block));
    public bool WillSkip() => StartSemicolon || !IsDialogue || Text is null || Text.Length == 0;

    private static ReadOnlySpan<char> GetEventStyleName(ReadOnlySpan<char> sp)
    {
        // https://sourceforge.net/p/guliverkli2/code/HEAD/tree/src/subtitles/STS.cpp#l1524
        // https://sourceforge.net/p/guliverkli2/code/HEAD/tree/src/subtitles/STS.cpp#l1490
        var spFixed = sp.TrimStart('*');
        if (spFixed.Length == 0 || MemoryExtensions.Equals(spFixed, "default", StringComparison.OrdinalIgnoreCase))
        {
            return "Default";
        }
        return spFixed;
    }
}
