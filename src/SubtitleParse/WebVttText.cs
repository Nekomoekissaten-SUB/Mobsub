using System.Text;
using Microsoft.Extensions.Logging;
using Mobsub.SubtitleParse.AssTypes;
using Mobsub.SubtitleParse.AssText;

namespace Mobsub.SubtitleParse;

public class WebVttText
{
    public bool CarriageReturn = false;
    public Encoding CharEncoding = Utils.EncodingRefOS();
    public VttFrame[] VttFrames = [];

    public struct VttFrame
    {
        public string? Id { get; set; }
        public AssTime StartTime { get; set; }
        public AssTime EndTime { get; set; }
        public string[] Text { get; set; }
    }

    public WebVttText ReadVttFile(Stream fs)
    {
        Utils.GuessEncoding(fs, out CharEncoding, out CarriageReturn);
        using var sr = new StreamReader(fs, CharEncoding, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
        VttFrames = Parse(sr).ToArray();
        return this;
    }

    public WebVttText ReadVttFile(string filePath)
    {
        using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read);
        return ReadVttFile(fs);
    }

    internal static IEnumerable<VttFrame> Parse(StreamReader sr)
    {
        string? line;
        var inHeader = true;
        var headerChecked = false;

        while ((line = sr.ReadLine()) != null)
        {
            if (inHeader)
            {
                if (!headerChecked)
                {
                    headerChecked = true;
                    if (line.StartsWith("WEBVTT", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }
                }

                if (line.Length == 0)
                {
                    inHeader = false;
                    continue;
                }

                if (line.StartsWith("NOTE", StringComparison.OrdinalIgnoreCase) ||
                    line.StartsWith("STYLE", StringComparison.OrdinalIgnoreCase) ||
                    line.StartsWith("REGION", StringComparison.OrdinalIgnoreCase))
                {
                    SkipBlock(sr);
                    inHeader = false;
                    continue;
                }

                inHeader = false;
            }

            if (line.Length == 0)
            {
                continue;
            }

            if (line.StartsWith("NOTE", StringComparison.OrdinalIgnoreCase) ||
                line.StartsWith("STYLE", StringComparison.OrdinalIgnoreCase) ||
                line.StartsWith("REGION", StringComparison.OrdinalIgnoreCase))
            {
                SkipBlock(sr);
                continue;
            }

            string? cueId = null;
            string timeLine = line;
            if (!IsTimeLine(timeLine))
            {
                cueId = timeLine;
                timeLine = sr.ReadLine() ?? string.Empty;
            }

            if (!IsTimeLine(timeLine))
            {
                continue;
            }

            ParseTimeLine(timeLine.AsSpan(), out var start, out var end);

            List<string> text = [];
            while ((line = sr.ReadLine()) != null && line.Length > 0)
            {
                text.Add(line);
            }

            yield return new VttFrame
            {
                Id = cueId,
                StartTime = start,
                EndTime = end,
                Text = text.ToArray(),
            };
        }
    }

    public void WriteVttFile(string filePath, bool forceEnv)
    {
        var charEncoding = forceEnv ? Utils.EncodingRefOS() : CharEncoding;

        using FileStream fileStream = new(filePath, FileMode.Create, FileAccess.Write);
        using var memStream = new MemoryStream();
        using var sw = new StreamWriter(memStream, charEncoding);

        var newline = forceEnv ? Environment.NewLine : (CarriageReturn ? "\r\n" : "\n");
        sw.Write("WEBVTT");
        sw.Write(newline);
        sw.Write(newline);

        foreach (var frame in VttFrames)
        {
            if (!string.IsNullOrEmpty(frame.Id))
            {
                sw.Write(frame.Id);
                sw.Write(newline);
            }

            sw.Write($"{WriteTime(frame.StartTime)} --> {WriteTime(frame.EndTime)}");
            sw.Write(newline);
            foreach (var l in frame.Text)
            {
                sw.Write(l);
                sw.Write(newline);
            }
            sw.Write(newline);
        }

        sw.Flush();
        memStream.Seek(0, SeekOrigin.Begin);
        memStream.CopyTo(fileStream);
        fileStream.Close();
    }

    public WebVttText FromAss(AssData ass)
    {
        List<VttFrame> frames = [];
        List<string> text = [];
        var index = 1;

        if (ass.Events == null)
        {
            VttFrames = [];
            return this;
        }

        foreach (var evt in ass.Events.Collection)
        {
            if (evt.StartSemicolon || !evt.IsDialogue)
                continue;

            ReadOnlyMemory<byte> textMemory;
            if (!evt.LineRaw.IsEmpty)
            {
                textMemory = evt.LineRaw[evt.TextReadOnly];
            }
            else
            {
                var textValue = evt.Text;
                if (string.IsNullOrEmpty(textValue))
                    continue;
                textMemory = Encoding.UTF8.GetBytes(textValue);
            }

            if (textMemory.IsEmpty)
                continue;

            text.Clear();
            var builder = new StringBuilder();

            AssEventTextParser.WithParsedSegments(textMemory, (segments, lineSpan) =>
            {
                foreach (var seg in segments)
                {
                    switch (seg.SegmentKind)
                    {
                        case AssEventSegmentKind.TagBlock:
                            break;
                        case AssEventSegmentKind.Text:
                            builder.Append(Utils.GetString(lineSpan[seg.LineRange]));
                            break;
                        case AssEventSegmentKind.HardLineBreaker:
                        case AssEventSegmentKind.SoftLineBreaker:
                            FlushLine(builder, text);
                            break;
                        case AssEventSegmentKind.NonBreakingSpace:
                            builder.Append('\u00A0');
                            break;
                    }
                }
            });

            FlushLine(builder, text);

            frames.Add(new VttFrame
            {
                Id = index.ToString(),
                StartTime = evt.Start,
                EndTime = evt.End,
                Text = text.ToArray(),
            });
            index++;
        }

        CarriageReturn = ass.CarriageReturn;
        CharEncoding = ass.CharEncoding;
        VttFrames = frames.ToArray();
        return this;
    }

    public AssData ToAssData(ILogger? logger = null)
    {
        var ass = new AssData(logger);
        ass.CharEncoding = CharEncoding;
        ass.CarriageReturn = CarriageReturn;
        ass.Sections = [AssSection.ScriptInfo, AssSection.StylesV4P, AssSection.Events];

        ass.ScriptInfo.ScriptType = AssConstants.ScriptTypeV4P;
        ass.ScriptInfo.Orders.Add(AssConstants.ScriptInfo.ScriptType);
        ass.ScriptInfo.Orders.Add(AssConstants.ScriptInfo.PlayResX);
        ass.ScriptInfo.Orders.Add(AssConstants.ScriptInfo.PlayResY);
        ass.ScriptInfo.Orders.Add(AssConstants.ScriptInfo.WrapStyle);

        ass.Styles = new AssStyles(logger);
        var defaultStyle = new AssStyle(AssConstants.StyleDefaultV4P, AssConstants.StylesLineHeaders.Style, ass.Styles.Formats);
        ass.Styles.Collection.Add(defaultStyle);
        ass.Styles.Names.Add(defaultStyle.Name);
        ass.Styles.InvalidateStyleMap();

        ass.Events = new AssEvents(logger);
        int lineNumber = 1;
        foreach (var frame in VttFrames)
        {
            var text = JoinLines(frame.Text);
            var line = $"Dialogue: 0,{FormatAssTime(frame.StartTime)},{FormatAssTime(frame.EndTime)},{AssConstants.StyleNames.DefaultString},,0,0,0,,{text}";
            var lineBytes = Encoding.UTF8.GetBytes(line);
            var evt = new AssEvent(lineBytes, lineNumber, AssConstants.EventsLineHeaders.Dialogue, ass.Events.Formats);
            ass.Events.Collection.Add(evt);
            lineNumber++;
        }

        return ass;
    }

    private static string JoinLines(string[] lines)
        => lines.Length == 0 ? string.Empty : string.Join(AssConstants.Text.AssHardLineBreak, lines);

    private static void FlushLine(StringBuilder builder, List<string> lines)
    {
        if (builder.Length == 0)
            return;

        lines.Add(builder.ToString().Trim());
        builder.Clear();
    }

    private static bool IsTimeLine(ReadOnlySpan<char> line)
        => line.IndexOf("-->", StringComparison.Ordinal) >= 0;

    private static void SkipBlock(StreamReader sr)
    {
        string? line;
        while ((line = sr.ReadLine()) != null && line.Length > 0) { }
    }

    private static void ParseTimeLine(ReadOnlySpan<char> line, out AssTime start, out AssTime end)
    {
        int arrowIndex = line.IndexOf("-->", StringComparison.Ordinal);
        if (arrowIndex < 0)
        {
            start = default;
            end = default;
            return;
        }

        var startSpan = line[..arrowIndex].Trim();
        var endSpan = line[(arrowIndex + 3)..].Trim();
        int endSpace = endSpan.IndexOf(' ');
        if (endSpace >= 0)
            endSpan = endSpan[..endSpace];

        start = ParseVttTime(startSpan);
        end = ParseVttTime(endSpan);
    }

    private static AssTime ParseVttTime(ReadOnlySpan<char> span)
    {
        span = span.Trim();
        int dotIndex = span.IndexOf('.');
        if (dotIndex < 0)
            throw new FormatException($"Invalid VTT time format: {span.ToString()}");

        var main = span[..dotIndex];
        var msSpan = span[(dotIndex + 1)..];

        int ms = 0;
        if (msSpan.Length >= 3)
        {
            ms = int.Parse(msSpan[..3]);
        }
        else if (msSpan.Length > 0)
        {
            ms = int.Parse(msSpan);
            if (msSpan.Length == 1)
                ms *= 100;
            else if (msSpan.Length == 2)
                ms *= 10;
        }

        int firstColon = main.IndexOf(':');
        if (firstColon < 0)
            throw new FormatException($"Invalid VTT time format: {span.ToString()}");

        int secondColon = main[(firstColon + 1)..].IndexOf(':');
        int hours;
        int minutes;
        int seconds;
        if (secondColon < 0)
        {
            hours = 0;
            minutes = int.Parse(main[..firstColon]);
            seconds = int.Parse(main[(firstColon + 1)..]);
        }
        else
        {
            hours = int.Parse(main[..firstColon]);
            minutes = int.Parse(main.Slice(firstColon + 1, secondColon));
            seconds = int.Parse(main[(firstColon + 1 + secondColon + 1)..]);
        }

        var totalMs = ((hours * 3600) + (minutes * 60) + seconds) * 1000 + ms;
        return new AssTime(totalMs);
    }

    private static string WriteTime(AssTime timeSpan)
    {
        return $"{timeSpan.Hour:D2}:{timeSpan.Minute:D2}:{timeSpan.Second:D2}.{timeSpan.Millisecond:D3}";
    }

    private static string FormatAssTime(AssTime time)
    {
        var sb = new StringBuilder();
        AssTime.WriteAssTime(sb, time, ctsRounding: false);
        return sb.ToString();
    }
}
