using System.Text;
using Microsoft.Extensions.Logging;
using ZLogger;

namespace Mobsub.SubtitleParseNT2.AssTypes;

public class AssEvents(ILogger? logger = null)
{
    private string[]? formats;
    public string[] Formats
    {
        get => formats ?? [.. AssConstants.EventFormatV4P.Split(',').Select(s => s.Trim())];
        set => formats = value;
    }
    public List<AssEvent> Collection = [];

    public Action<AssEvent>? OnEventParsed { get; set; }

    public void Read(ReadOnlyMemory<byte> line, ReadOnlySpan<byte> scriptType, int lineNumber)
    {
        var sp = line.Span;
        if (sp[0] == ';')
        {
            var evt = new AssEvent(line, lineNumber, ";"u8, Formats);
            Dispatch(evt);
            return;
        }

        var sepIndex = sp.IndexOf((byte)':');

        if (sp[..sepIndex].SequenceEqual("Format"u8))
        {
            if (scriptType.SequenceEqual("v4.00++"u8))
                throw new Exception($"Events: {AssConstants.ScriptTypeV4PP} not have format line");
            Formats = ParseFormatLine(sp[(sepIndex + 1)..]);
            if (!Formats[^1].AsSpan().SequenceEqual("Text".AsSpan()))
                throw new Exception("Events: Text must be last field.");
            logger?.ZLogDebug($"Events: Parse format line fine");
        }
        else
        {
            var evt = new AssEvent(line, lineNumber, sp[..sepIndex], Formats);
            Dispatch(evt);
        }
    }

    private void Dispatch(AssEvent evt)
    {
        Collection.Add(evt);
        OnEventParsed?.Invoke(evt);
    }

    private static string[] ParseFormatLine(ReadOnlySpan<byte> line)
    {
        var results = new List<string>();
        int start = 0;
        while (start < line.Length)
        {
            int comma = line.Slice(start).IndexOf((byte)',');
            if (comma == -1) comma = line.Length - start;

            var segment = Utils.TrimSpaces(line.Slice(start, comma));
            if (!segment.IsEmpty)
                results.Add(Encoding.UTF8.GetString(segment.ToArray()));

            start += comma + 1;
        }
        return [.. results];
    }

    public void Write(StreamWriter sw, char[] newline)
    {
        sw.Write(AssConstants.SectionEvent);
        sw.Write(newline);
        sw.Write($"Format: {string.Join(", ", Formats)}");
        sw.Write(newline);
        foreach (var evt in Collection)
        {
            Helper.Write(sw, evt, Formats, true);
            sw.Write(newline);
        }
    }

}
