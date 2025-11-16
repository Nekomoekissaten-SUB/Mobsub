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

    private Dictionary<byte[], AssStyleView>.AlternateLookup<ReadOnlySpan<byte>>? _styleViewMap;
    public Dictionary<byte[], AssStyleView>.AlternateLookup<ReadOnlySpan<byte>> StyleViewMap => _styleViewMap ??= BuildStyleViewDictionary();
    private AssStyleView? _defaultView;
    public AssStyleView DefaultStyleView => _defaultView ??= new(AssConstants.StyleDefaultV4P, "Style"u8, Formats, logger);

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

    private Dictionary<byte[], AssStyleView>.AlternateLookup<ReadOnlySpan<byte>> BuildStyleViewDictionary()
    {
        var dict = new Dictionary<byte[], AssStyleView>(Utf8StringEqualityComparer.Default);
        foreach (var s in Collection)
        {
            var view = s.GetView();
            var nameSpan = view.NameSpan;
            // https://sourceforge.net/p/guliverkli2/code/HEAD/tree/src/subtitles/STS.cpp#l1447
            if (!nameSpan.IsEmpty && nameSpan[0] == (byte)'*')
                nameSpan = nameSpan[1..];
            dict[nameSpan.ToArray()] = view;
        }
        return dict.GetAlternateLookup<ReadOnlySpan<byte>>();
    }

    public bool TryGetAssStyleViewByEventStyle(ReadOnlySpan<byte> styleName, out ReadOnlySpan<byte> queryName, out AssStyleView? view)
    {
        // https://sourceforge.net/p/guliverkli2/code/HEAD/tree/src/subtitles/STS.cpp#l1490
        while (!styleName.IsEmpty && styleName[0] == (byte)'*')
            styleName = styleName[1..];

        var defLower = "default"u8;
        if (styleName.Length == defLower.Length)
        {
            bool isDefault = true;
            for (int i = 0; i < defLower.Length; i++)
            {
                byte b = styleName[i];
                if (b < 0x80)
                {
                    if ((byte)(b | 0x20) != defLower[i])
                    {
                        isDefault = false;
                        break;
                    }
                }
                else
                {
                    isDefault = false;
                    break;
                }
            }

            if (isDefault)
            {
                styleName = "Default"u8;
            }
        }

        queryName = styleName;
        return TryGetAssStyleViewByName(queryName, out view);
    }
    public bool TryGetAssStyleViewByName(ReadOnlySpan<byte> styleName, out AssStyleView? view)
    {
        // https://sourceforge.net/p/guliverkli2/code/HEAD/tree/src/subtitles/RTS.cpp#l1857
        if (StyleViewMap.TryGetValue(styleName, out view))
        {
            return true;
        }

        if (StyleViewMap.TryGetValue("Default"u8, out view))
        {
            return false;
        }
        return false;
    }
    public AssStyleView GetAssStyleViewByEventStyle(ReadOnlySpan<byte> styleName)
    {
        var result = TryGetAssStyleViewByEventStyle(styleName, out _, out var view);
        return TryGetAssStyleViewProcessResult(result, view, styleName);
    }
    public AssStyleView GetAssStyleViewByName(ReadOnlySpan<byte> styleName)
    {
        var result = TryGetAssStyleViewByName(styleName, out var view);
        return TryGetAssStyleViewProcessResult(result, view, styleName);
    }
    private AssStyleView TryGetAssStyleViewProcessResult(bool notFallback, AssStyleView? view, ReadOnlySpan<byte> styleName)
    {
        if (notFallback)
        {
            return view!;
        }
        logger?.ZLogWarning($"Events: Style '{Utils.GetString(styleName)}' not found, will fallback to 'Default' style.");
        if (view is null)
        {
            logger?.ZLogWarning($"Events: 'Default' style also not found, will generate a 'Default' style.");
            return DefaultStyleView;
        }
        else
        {
            return view;
        }
    }

}
