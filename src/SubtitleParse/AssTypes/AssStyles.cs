﻿using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Extensions.Logging;
using ZLogger;

namespace Mobsub.SubtitleParse.AssTypes;

public class AssStyles(ILogger? logger = null)
{
    private static readonly string[] DefaultFormats = ParseFormats(AssConstants.StyleFormatV4P.AsSpan());
    private string[]? formats;
    public string[] Formats
    {
        get => formats ??= [.. DefaultFormats];
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
            Formats = ParseFormats(sp[(sepIndex + 1)..]);
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

    private static string[] ParseFormats(ReadOnlySpan<byte> span)
    {
        int count = 1;
        for (int i = 0; i < span.Length; i++)
        {
            if (span[i] == (byte)',')
                count++;
        }

        var result = new string[count];
        int start = 0;
        int index = 0;

        for (int i = 0; i <= span.Length; i++)
        {
            if (i == span.Length || span[i] == (byte)',')
            {
                var token = span.Slice(start, i - start);
                int tokenStart = 0;
                int tokenEnd = token.Length;
                while (tokenStart < tokenEnd && IsFormatSpace(token[tokenStart]))
                    tokenStart++;
                while (tokenEnd > tokenStart && IsFormatSpace(token[tokenEnd - 1]))
                    tokenEnd--;

                result[index++] = tokenEnd > tokenStart
                    ? Utils.GetString(token.Slice(tokenStart, tokenEnd - tokenStart))
                    : string.Empty;
                start = i + 1;
            }
        }

        return result;
    }

    private static bool IsFormatSpace(byte b) => b == (byte)' ' || b == (byte)'\t';

    private static string[] ParseFormats(ReadOnlySpan<char> span)
    {
        int count = 1;
        for (int i = 0; i < span.Length; i++)
        {
            if (span[i] == ',')
                count++;
        }

        var result = new string[count];
        int start = 0;
        int index = 0;

        for (int i = 0; i <= span.Length; i++)
        {
            if (i == span.Length || span[i] == ',')
            {
                var token = span.Slice(start, i - start);
                int tokenStart = 0;
                int tokenEnd = token.Length;
                while (tokenStart < tokenEnd && IsFormatSpace(token[tokenStart]))
                    tokenStart++;
                while (tokenEnd > tokenStart && IsFormatSpace(token[tokenEnd - 1]))
                    tokenEnd--;

                result[index++] = tokenEnd > tokenStart
                    ? token.Slice(tokenStart, tokenEnd - tokenStart).ToString()
                    : string.Empty;
                start = i + 1;
            }
        }

        return result;
    }

    private static bool IsFormatSpace(char c) => c == ' ' || c == '\t';

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
    public void Write(StreamWriter sw, char[] newline, AssSection section = AssSection.StylesV4P)
    {
        sw.Write(AssConstants.SectionStyleV4P);
        sw.Write(section switch
        {
            AssSection.StylesV4 => "[V4 Styles]",
            AssSection.StylesV4PP => "[V4++ Styles]",
            _ => AssConstants.SectionStyleV4P
        });
        sw.Write(newline);
        sw.Write($"Format: {string.Join(", ", Formats)}");
        sw.Write(newline);
        foreach (var style in Collection)
        {
            style.Write(sw, Formats);
            sw.Write(newline);
        }
    }

    public AssStyles DeepClone()
    {
        var clone = new AssStyles();
        clone.Formats = [.. Formats];
        clone.Collection = new List<AssStyle>(Collection);
        clone.Names = new HashSet<string>(Names);
        clone.InvalidateStyleMap();
        return clone;
    }

}
