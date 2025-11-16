using Microsoft.Extensions.Logging;
using System.Text;
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
    public List<AssEventHandle> Collection = [];

    public Action<AssEventView>? OnEventView { get; set; }

    public void Read(ReadOnlyMemory<byte> line, ReadOnlySpan<byte> scriptType, int lineNumber)
    {
        var sp = line.Span;
        if (sp[0] == ';')
        {
            var view = new AssEventView(line, lineNumber, ";"u8, Formats, logger);
            Dispatch(view);
            return;
        }

        var sepIndex = sp.IndexOf((byte)':');

        if (sp[..sepIndex].SequenceEqual("Format"u8))
        {
            if (scriptType.SequenceEqual("v4.00++"u8))
                throw new Exception($"Events: {AssConstants.ScriptTypeV4PP} not have format line");
            Formats = Encoding.UTF8.GetString(sp[(sepIndex + 1)..].ToArray()).Split(',').Select(s => s.Trim()).ToArray();
            if (!Formats[^1].AsSpan().SequenceEqual("Text".AsSpan()))
                throw new Exception("Events: Text must be last field.");
            logger?.ZLogDebug($"Events: Parse format line fine");
        }
        else
        {
            var view = new AssEventView(line, lineNumber, sp[..sepIndex], Formats, logger);
            Dispatch(view);
        }
    }

    private void Dispatch(AssEventView view)
    {
        if (OnEventView == null)
        {
            Collection.Add(new AssEventHandle(view));
        }
        else
        {
            OnEventView(view);
        }
    }

}
