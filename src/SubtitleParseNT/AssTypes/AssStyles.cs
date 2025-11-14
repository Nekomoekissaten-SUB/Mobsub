using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Text;
using ZLogger;

namespace Mobsub.SubtitleParseNT2.AssTypes;

public class AssStyles(ILogger? logger = null)
{
    private string[]? formats;
    public string[] Formats
    {
        get => formats ?? [.. AssConstants.StyleFormatV4P.Split(',').Select(s => s.Trim())];
        set => formats = value;
    }
    public List<AssStyleHandle> Collection = [];


    public void Read(ReadOnlyMemory<byte> line, int lineNumber)
    {
        var sp = line.Span;
        if (sp[0] == '/')
        {
            var view = new AssStyleView(line, "/"u8, Formats, logger);
            Collection.Add(new AssStyleHandle(view));
            return;
        }

        var sepIndex = sp.IndexOf((byte)':');

        if (sp[..sepIndex].SequenceEqual("Format"u8))
        {
            Formats = Encoding.UTF8.GetString(sp[(sepIndex + 1)..].ToArray()).Split(',').Select(s => s.Trim()).ToArray();
            logger?.ZLogDebug($"Styles: Line {lineNumber} is a format line, parse completed");
        }
        else
        {
            var view = new AssStyleView(line, sp[..sepIndex], Formats, logger);
            Collection.Add(new AssStyleHandle(view));
        }
    }

    public Dictionary<string, AssStyleView> BuildStyleViewDictionary()
    {
        var dict = new Dictionary<string, AssStyleView>(Collection.Count, StringComparer.Ordinal);
        foreach (var s in Collection)
        {
            var view = s.GetView();
            var name = Encoding.UTF8.GetString(view.NameSpan);
            dict[name] = view;
        }
        return dict;
    }

}
