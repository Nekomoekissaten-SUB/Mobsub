using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Extensions.Logging;
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
    public List<AssStyle> Collection = [];
    public HashSet<string> Names = [];

    // Rebuild on every access to guarantee consistency even if external code mutates Collection.
    private Dictionary<byte[], AssStyle>? _styleDict;
    private bool _styleMapDirty = true;
    public Dictionary<byte[], AssStyle>.AlternateLookup<ReadOnlySpan<byte>> StyleMap
    {
        get
        {
            EnsureStyleMap();
            return _styleDict!.GetAlternateLookup<ReadOnlySpan<byte>>();
        }
    }
    private AssStyle? _defaultStyle;
    public AssStyle DefaultStyle => _defaultStyle ??= new(AssConstants.StyleDefaultV4P, "Style"u8, Formats);

    public void Read(ReadOnlyMemory<byte> line, int lineNumber)
    {
        var sp = line.Span;
        if (sp[0] == '/')
        {
            var style = new AssStyle(line, "/"u8, Formats);
            Collection.Add(style);
            return;
        }

        var sepIndex = sp.IndexOf((byte)':');

        if (sp[..sepIndex].SequenceEqual("Format"u8))
        {
            Formats = Utils.GetString(sp[(sepIndex + 1)..]).Split(',').Select(s => s.Trim()).ToArray();
            logger?.ZLogDebug($"Styles: Line {lineNumber} is a format line, parse completed");
        }
        else
        {
            var style = new AssStyle(line, sp[..sepIndex], Formats);
            Names.Add(style.Name);
            Collection.Add(style);
            _styleMapDirty = true;
        }
    }

    private Dictionary<byte[], AssStyle> BuildStyleDictionary()
    {
        var dict = new Dictionary<byte[], AssStyle>(Utf8StringEqualityComparer.Default);
        foreach (var s in Collection)
        {
            var nameSpan = s.NameSpan;
            // https://sourceforge.net/p/guliverkli2/code/HEAD/tree/src/subtitles/STS.cpp#l1447
            if (!nameSpan.IsEmpty && nameSpan[0] == (byte)'*')
                nameSpan = nameSpan[1..];
            dict[nameSpan.ToArray()] = s;
        }
        return dict;
    }

    private void EnsureStyleMap()
    {
        if (_styleDict == null || _styleMapDirty)
        {
            _styleDict = BuildStyleDictionary();
            _styleMapDirty = false;
        }
    }

    public void InvalidateStyleMap() => _styleMapDirty = true;

    public bool TryGetAssStyleByEventStyle(ReadOnlySpan<byte> styleName, out ReadOnlySpan<byte> queryName, out AssStyle view)
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
        return TryGetAssStyleByName(queryName, out view);
    }
    public bool TryGetAssStyleByName(ReadOnlySpan<byte> styleName, out AssStyle view)
    {
        // https://sourceforge.net/p/guliverkli2/code/HEAD/tree/src/subtitles/RTS.cpp#l1857
        if (StyleMap.TryGetValue(styleName, out view))
        {
            return true;
        }

        if (StyleMap.TryGetValue("Default"u8, out view))
        {
            return false;
        }
        view = default;
        return false;
    }
    public AssStyle GetAssStyleByEventStyle(ReadOnlySpan<byte> styleName)
    {
        var result = TryGetAssStyleByEventStyle(styleName, out _, out var view);
        return TryGetAssStyleProcessResult(result, view, styleName);
    }
    public AssStyle GetAssStyleByName(ReadOnlySpan<byte> styleName)
    {
        var result = TryGetAssStyleByName(styleName, out var view);
        return TryGetAssStyleProcessResult(result, view, styleName);
    }
    private AssStyle TryGetAssStyleProcessResult(bool notFallback, AssStyle view, ReadOnlySpan<byte> styleName)
    {
        if (notFallback)
        {
            return view;
        }
        logger?.ZLogWarning($"Events: Style '{Utils.GetString(styleName)}' not found, will fallback to 'Default' style.");
        
        if (view.LineRaw.Length > 0) // crude check for valid struct
        {
             return view;
        }

        logger?.ZLogWarning($"Events: 'Default' style also not found, will generate a 'Default' style.");
        return DefaultStyle;
    }

    public void Write(StreamWriter sw, char[] newline)
    {
        sw.Write(AssConstants.SectionStyleV4P);
        sw.Write(newline);
        sw.Write($"Format: {string.Join(", ", Formats)}");
        sw.Write(newline);
        foreach (var style in Collection)
        {
            style.Write(sw, Formats);
            sw.Write(newline);
        }
    }

}
