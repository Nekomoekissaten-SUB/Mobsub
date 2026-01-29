﻿using System.Text;
using Microsoft.Extensions.Logging;
using Mobsub.SubtitleParse.AssUtils;
using ZLogger;

namespace Mobsub.SubtitleParse.AssTypes;

public class AssEvents(ILogger? logger = null)
{
    private static readonly string[] DefaultFormats = [.. AssConstants.EventFormatV4P.Split(',').Select(s => s.Trim())];
    private string[]? formats;
    public string[] Formats
    {
        get => formats ??= [.. DefaultFormats];
        set => formats = value;
    }
    public List<AssEvent> Collection = [];

    public Action<AssEvent>? OnEventParsed { get; set; }

    public bool ExtractAegisubExtradataMarkers { get; set; }
    public bool PreserveAegisubExtradataMarkersOnWrite { get; set; } = true;

    public void Read(ReadOnlyMemory<byte> line, ReadOnlySpan<byte> scriptType, int lineNumber)
    {
        var sp = line.Span;
        if (sp[0] == AssConstants.CommentLinePrefixByte)
        {
            var evt = new AssEvent(line, lineNumber, AssConstants.EventsLineHeaders.Semicolon, Formats);
            Dispatch(evt);
            return;
        }

        var sepIndex = sp.IndexOf((byte)':');

        if (sp[..sepIndex].SequenceEqual(AssConstants.EventsLineHeaders.Format))
        {
            if (scriptType.SequenceEqual(AssConstants.ScriptTypeBytes.V4PP))
                throw new Exception($"Events: {AssConstants.ScriptTypeV4PP} not have format line");
            Formats = ParseFormatLine(sp[(sepIndex + 1)..]);
            if (!Formats[^1].AsSpan().SequenceEqual(AssConstants.EventFields.Text.AsSpan()))
                throw new Exception($"Events: {AssConstants.EventFields.Text} must be last field.");
            logger?.ZLogDebug($"Events: Parse format line fine");
        }
        else
        {
            var evt = new AssEvent(line, lineNumber, sp[..sepIndex], Formats);
            if (ExtractAegisubExtradataMarkers)
                evt.TryExtractAegisubExtradataMarkerFromText();
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
                results.Add(Utils.GetString(segment));

            start += comma + 1;
        }
        return [.. results];
    }

    public void Write(StreamWriter sw, char[] newline, bool ctsRounding)
    {
        sw.Write(AssConstants.SectionEvent);
        sw.Write(newline);
        sw.Write($"Format: {string.Join(", ", Formats)}");
        sw.Write(newline);
        foreach (var evt in Collection)
        {
            Helper.Write(sw, evt, Formats, ctsRounding, includeAegisubExtradataMarker: PreserveAegisubExtradataMarkersOnWrite);
            sw.Write(newline);
        }
    }

}
