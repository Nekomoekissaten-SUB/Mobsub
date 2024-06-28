using System.Text;
using Mobsub.SubtitleParse.AssTypes;

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

    public SubRipText ReadSrtFile(FileStream fs)
    {
        using var sr = new StreamReader(fs);
        Utils.GuessEncoding(fs, out CharEncoding, out CarriageReturn);
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
                var timeCodeLine = sr.ReadLine().AsSpan();
                var startTime = ParseTime(timeCodeLine, 0);
                var endTime = ParseTime(timeCodeLine, 17);
                
                lines.Clear();
                while ((line = sr.ReadLine()) != null && !string.IsNullOrEmpty(line))
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
        List<SrtFrame> sf = [];
        List<string> text = [];
        StringBuilder sb = new();
        var index = 1;
        foreach (var evt in ass.Events.Collection)
        {
            // skip comment and empty event lines
            // Now not support tag convert
            if (evt is not { StartSemicolon: false, IsDialogue: true, Text.Count: > 0 }) continue;
            foreach (var s in evt.Text)
            {
                var sp = s.AsSpan();

                if (AssTagParse.IsOverrideBlock(sp))
                {
                    //if (!ignoreTags)
                    //{
                    //    TagConvertToSrt(sp, sb);
                    //}
                }
                else if (sp.Length == 2 && sp[0] == AssConstants.BackSlash)
                {
                    switch (sp[1])
                    {
                        case AssConstants.LineBreaker:
                        case AssConstants.WordBreaker:
                            if (sb.Length > 0)
                                text.Add(sb.ToString().Trim()); sb.Clear();
                            break;
                        default: break;
                    }
                }
                else
                {
                    sb.Append(sp);
                }
            }
                
            if (sb.Length > 0)
                text.Add(sb.ToString().Trim()); sb.Clear();

            sf.Add(new SrtFrame()
            {
                Index = index,
                StartTime = evt.Start,
                EndTime = evt.End,
                Text = text.ToArray(),
            });
                
            index++;
            text.Clear();
        }

        CarriageReturn = ass.CarriageReturn;
        CharEncoding = ass.CharEncoding;
        SrtFrames = sf.ToArray();
        return this;
    }
    //private static void TagConvertToSrt(Span<char> sp, StringBuilder sb, AssStyle style)
    //{

    //}
}

