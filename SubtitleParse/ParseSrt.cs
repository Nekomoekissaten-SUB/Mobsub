using System.Text;
using Mobsub.Utils;
using Mobsub.AssTypes;

namespace Mobsub.SubtitleParse;

public struct SrtFrame
{
    public int Index { get; set; }
    public AssTime StartTime { get; set; }
    public AssTime EndTime { get; set; }
    public string Text { get; set; }
}

public class SubRipText
{
    public bool CarriageReturn = false;
    public Encoding CharEncoding = DetectEncoding.EncodingRefOS();
    public SrtFrame[] srtFrames = [];

    public SubRipText ReadSrtFile(FileStream fs, bool forceEnv)
    {
        using var sr = new StreamReader(fs);
        DetectEncoding.GuessEncoding(fs, out CharEncoding, out CarriageReturn);
        srtFrames = Parse(sr, forceEnv).ToArray();
        return this;
    }

    public SubRipText ReadSrtFile(FileStream fs) => ReadSrtFile(fs, false);

    public SubRipText ReadSrtFile(string filePath, bool forceEnv)
    {
        using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read);
        return ReadSrtFile(fs, forceEnv);
    }

    public SubRipText ReadSrtFile(string filePath) => ReadSrtFile(filePath, false);

    internal IEnumerable<SrtFrame> Parse(StreamReader sr, bool forceEnv)
    {
        string? line;
        StringBuilder sb = new();
        var newline = forceEnv ? [.. Environment.NewLine] : (CarriageReturn ? new char[]{'\r', '\n'} : ['\n']);
        while ((line = sr.ReadLine()) != null)
        {
            if (int.TryParse(line, out int index))
            {
                var timeCodeLine = sr.ReadLine().AsSpan();
                var startTime = ParseTime(timeCodeLine, 0);
                var endTime = ParseTime(timeCodeLine, 17);
                // List<string> lines = [];

                sb.Clear();
                while ((line = sr.ReadLine()) != null && !string.IsNullOrEmpty(line))
                {
                    sb.Append(line);
                    sb.Append(newline);
                    // lines.Add(line);
                }

                yield return new SrtFrame
                {
                    Index = index,
                    StartTime = startTime,
                    EndTime = endTime,
                    Text = sb.ToString(),
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
        var charEncoding = forceEnv ? DetectEncoding.EncodingRefOS() : CharEncoding;

        using FileStream fileStream = new(filePath, FileMode.Create, FileAccess.Write);
        using var memStream = new MemoryStream();
        using var sw = new StreamWriter(memStream, charEncoding);
        foreach (var frame in srtFrames)
        {
            sw.WriteLine(frame.Index);
            sw.WriteLine($"{WriteTime(frame.StartTime)} --> {WriteTime(frame.EndTime)}");
            sw.WriteLine(frame.Text);
            sw.WriteLine();
        }
    }
    private static string WriteTime(AssTime timeSpan)
    {
        return $"{timeSpan.Hour:D2}:{timeSpan.Minute:D2}:{timeSpan.Second:D2},{timeSpan.Millisecond:D3}";
    }

}

