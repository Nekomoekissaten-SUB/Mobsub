using System.Text;
using Mobsub.SubtitleParse.AssTypes;
using Mobsub.SubtitleParse.AssText;

namespace Mobsub.SubtitleParse;

public class SubRipText
{
    public bool CarriageReturn = false;
    public Encoding CharEncoding = Utils.EncodingRefOS();
    public SrtFrame[] SrtFrames = [];

    public struct SrtFrame
    {
        public int Index { get; set; }
        public AssTime StartTime { get; set; }
        public AssTime EndTime { get; set; }
        public string[] Text { get; set; }
    }

    public SubRipText ReadSrtFile(Stream fs)
    {
        Utils.GuessEncoding(fs, out CharEncoding, out CarriageReturn);
        using var sr = new StreamReader(fs, CharEncoding, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
        SrtFrames = Parse(sr).ToArray();
        return this;
    }

    public SubRipText ReadSrtFile(string filePath)
    {
        using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read);
        return ReadSrtFile(fs);
    }

    internal static IEnumerable<SrtFrame> Parse(StreamReader sr)
    {
        string? line;
        List<string> lines = [];
        while ((line = sr.ReadLine()) != null)
        {
            if (int.TryParse(line, out int index))
            {
                var timeCodeLine = sr.ReadLine();
                if (timeCodeLine == null)
                {
                    yield break;
                }

                var timeSpan = timeCodeLine.AsSpan();
                var startTime = ParseTime(timeSpan, 0);
                var endTime = ParseTime(timeSpan, 17);

                lines.Clear();
                while ((line = sr.ReadLine()) != null && line.Length > 0)
                {
                    lines.Add(line);
                }

                yield return new SrtFrame
                {
                    Index = index,
                    StartTime = startTime,
                    EndTime = endTime,
                    Text = lines.ToArray(),
                };
            }
        }
    }

    private static AssTime ParseTime(ReadOnlySpan<char> timeCodeLine, int startIndex)
    {
        var totalMs = 0;
        totalMs += int.Parse(timeCodeLine.Slice(startIndex, 2)) * 3600000;
        totalMs += int.Parse(timeCodeLine.Slice(startIndex + 3, 2)) * 60000;
        totalMs += int.Parse(timeCodeLine.Slice(startIndex + 6, 2)) * 1000;
        totalMs += int.Parse(timeCodeLine.Slice(startIndex + 9, 3));
        return new AssTime(totalMs);
    }

    public void WriteSrtFile(string filePath, bool forceEnv)
    {
        var charEncoding = forceEnv ? Utils.EncodingRefOS() : CharEncoding;

        using FileStream fileStream = new(filePath, FileMode.Create, FileAccess.Write);
        using var memStream = new MemoryStream();
        using var sw = new StreamWriter(memStream, charEncoding);
        foreach (var frame in SrtFrames)
        {
            sw.WriteLine(frame.Index);
            sw.WriteLine($"{WriteTime(frame.StartTime)} --> {WriteTime(frame.EndTime)}");
            foreach (var l in frame.Text)
                sw.WriteLine(l);
            sw.WriteLine();
        }
        sw.Flush();

        memStream.Seek(0, SeekOrigin.Begin);
        memStream.CopyTo(fileStream);
        fileStream.Close();
    }

    private static string WriteTime(AssTime timeSpan)
    {
        return $"{timeSpan.Hour:D2}:{timeSpan.Minute:D2}:{timeSpan.Second:D2},{timeSpan.Millisecond:D3}";
    }

    public SubRipText FromAss(AssData ass)
    {
        List<SrtFrame> frames = [];
        List<string> text = [];
        var index = 1;

        if (ass.Events == null)
        {
            SrtFrames = [];
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

            frames.Add(new SrtFrame
            {
                Index = index,
                StartTime = evt.Start,
                EndTime = evt.End,
                Text = text.ToArray(),
            });
            index++;
        }

        CarriageReturn = ass.CarriageReturn;
        CharEncoding = ass.CharEncoding;
        SrtFrames = frames.ToArray();
        return this;
    }

    private static void FlushLine(StringBuilder builder, List<string> lines)
    {
        if (builder.Length == 0)
            return;

        lines.Add(builder.ToString().Trim());
        builder.Clear();
    }
}
