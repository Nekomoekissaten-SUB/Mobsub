using System.Text;
using Mobsub.SubtitleParse.AssTypes;

namespace Mobsub.SubtitleParse;

public class AssFontParse
{
    /// <summary>
    /// Get ass font info as string and used glyphs from ass data.
    /// Need to parse info string based on font file. Ass font encoding shoule be ingore.
    /// </summary>
    /// <param name="events">event line collection</param>
    /// <param name="styles">style collection</param>
    /// <returns>key is a string order by "font_used_name,font_weight,font_italic,font_encoding" (1 = true, 0 = false), value is rune collection</returns>
    public static Dictionary<string, List<Rune>> GetUsedFonts(List<AssEvent> events, List<AssStyle> styles)
    {
        Dictionary<string, List<Rune>> usedFontGlyphs = [];
        var lineNumberFirst = events.First().lineNumber;
        
        foreach (var eventLine in events)
        {
            if (eventLine.IsDialogue)
            {
                var eventStyle = GetStyleByName(styles, eventLine.Style);
                var text = eventLine.Text.ToArray();
                var lineNumber = eventLine.lineNumber;

                var fn = new StringBuilder().Append(eventStyle.Fontname);
                var fe = new StringBuilder().Append(eventStyle.Encoding);
                var italic = new StringBuilder().Append(eventStyle.Italic ? '1' : '0');
                var weight = new StringBuilder().Append(eventStyle.Bold ? '1' : '0');
                List<Rune> runes = [];
                
                int step;
                for (var i = 0; i < text.Length; i += step)
                {
                    step = 1;
                    var slice = text[i].AsSpan();

                    if (slice.Length > 0 && slice[0] == '{' && slice[^1] == '}')
                    {
                        if (slice.Length > 2)
                        {
                            GetOverrideBlockFont(slice, eventStyle, styles, fn, fe, italic, weight, lineNumber, lineNumberFirst);
                        }
                        
                        if (i != text.Length - 1)
                        {
                            var sliceNext = text[i + 1].AsSpan();
                            DecodeCharsToRunes(sliceNext, runes);
                            RecordFontGlyphs(fn, fe, italic, weight, runes, usedFontGlyphs);
                            step += 1;
                        }
                    }
                    else if (slice.Length == 2 && AssConstants.IsEventSpecialCharPair(slice))
                    {
                        switch (slice[1])
                        {
                            case 'h':
                                runes.Add(new Rune(AssConstants.NBSP_Utf16));
                                RecordFontGlyphs(fn, fe, italic, weight, runes, usedFontGlyphs);
                                break;
                            default:
                                break;
                        }
                    }
                    else
                    {
                        DecodeCharsToRunes(slice, runes);
                        RecordFontGlyphs(fn, fe, italic, weight, runes, usedFontGlyphs);
                    }
                }
            }
        }
    
        return usedFontGlyphs;
    }

    /// <summary>
    /// Get AssStyle by styleName
    /// </summary>
    /// <param name="styles"></param>
    /// <param name="styleName"></param>
    /// <returns></returns>
    public static AssStyle GetStyleByName(List<AssStyle> styles, string styleName)
    {
        var sc = from st in styles where st.Name.Equals(styleName) select st;
        if (sc.Any())
        {
            return sc.Last();
        }
        else
        {
            sc = from st in styles where st.Name.Equals("Default") select st;
            if (sc.Any())
            {
                return sc.Last();
            }
            else
            {
                var style = new AssStyle();
                style.GetDefault();
                return style;
            }
        }
    }

    /// <summary>
    /// Parse font info from override tags (ovr block)
    /// </summary>
    /// <param name="tag"></param>
    /// <param name="eventStyle"></param>
    /// <param name="styles"></param>
    /// <param name="fn"></param>
    /// <param name="fe"></param>
    /// <param name="italic"></param>
    /// <param name="weight"></param>
    /// <param name="lineNumber"></param>
    /// <param name="lineNumberFirst"></param>
    private static void GetOverrideBlockFont(Span<char> tag, AssStyle eventStyle, List<AssStyle> styles, StringBuilder fn, StringBuilder fe, StringBuilder italic, StringBuilder weight, int lineNumber, int lineNumberFirst)
    {
        foreach (var ca in AssTagParse.GetTagsFromOvrBlock(tag))
        {
            GetOverrideFont(ca.AsSpan(), eventStyle, styles, fn, fe, italic, weight, lineNumber, lineNumberFirst);
        }
    }

    /// <summary>
    /// Parse font info from override tag (start without backslash)
    /// </summary>
    /// <param name="tag"></param>
    /// <param name="eventStyle"></param>
    /// <param name="styles"></param>
    /// <param name="fn"></param>
    /// <param name="fe"></param>
    /// <param name="italic"></param>
    /// <param name="weight"></param>
    /// <param name="lineNumber"></param>
    /// <param name="lineNumberFirst"></param>
    private static void GetOverrideFont(Span<char> tag, AssStyle eventStyle, List<AssStyle> styles, StringBuilder fn, StringBuilder fe, StringBuilder italic, StringBuilder weight, int lineNumber, int lineNumberFirst)
    {
        var len = tag.Length;
        if (tag.StartsWith("fn".AsSpan()))
        {
            fn.Clear();
            fn.Append(tag[2..]);
        }
        else if (tag.StartsWith("fe".AsSpan()))
        {
            fe.Clear();
            fe.Append(tag[2..]);
        }
        else if (tag.StartsWith("i".AsSpan()))
        {
            italic.Clear().Append(len > 1 ? tag[1] : '0');
        }
        else if (tag.StartsWith("r".AsSpan()))
        {
            if (len > 1)
            {
                var af = GetStyleByName(styles, new string(tag[1..len]));
                fn.Clear().Append(af.Fontname);
                weight.Clear().Append(af.Bold ? '1' : '0');
                italic.Clear().Append(af.Italic ? '1' : '0');
                fe.Clear().Append(af.Encoding);
            }
            else
            {
                fn.Clear().Append(eventStyle.Fontname);
                weight.Clear().Append(eventStyle.Bold ? '1' : '0');
                italic.Clear().Append(eventStyle.Italic ? '1' : '0');
                fe.Clear().Append(eventStyle.Encoding);
            }
        }
        else if (tag.StartsWith("b") && !(tag[1] is 'o' or 'l' or 'e'))
        {
            weight.Clear();
            if (len > 1)
            {
                if (char.IsDigit(tag[1]))
                {
                    foreach (var c in tag[1..len])
                    {
                        if (char.IsDigit(c))
                        {
                            weight.Append(c);
                        }
                        else
                        {
                            EventsIllegalTagException(new string(tag), lineNumber, lineNumberFirst);
                        }
                    }
                }
                else
                {
                    EventsIllegalTagException(new string(tag), lineNumber, lineNumberFirst);
                    // if (tag[1] == '+')
                    // {
                    //     foreach (var c in tag[2..len])
                    //     {
                    //         weight.Append(c);
                    //     }
                    // }
                    // else
                    // {
                    //     weight.Append('0');
                    // }
                }
            }
            else
            {
                weight.Append('0');
            }
        }
    }

    private static void RecordFontGlyphs(StringBuilder fn, StringBuilder fe, StringBuilder italic, StringBuilder weight, List<Rune> runes, Dictionary<string, List<Rune>> usedFontGlyphs)
    {
        var fontStr = new StringBuilder().Append(fn).Append(',').Append(weight).Append(',').Append(italic).Append(',').Append(fe).ToString();
        if (!usedFontGlyphs.TryGetValue(fontStr, out List<Rune>? _))
        {
            usedFontGlyphs.Add(fontStr, []);
        }
    
        foreach (var r in runes)
        {
            if (!usedFontGlyphs[fontStr].Contains(r))
            {
                usedFontGlyphs[fontStr].Add(r);
            }
        }

        runes.Clear();
    }

    private static void DecodeCharsToRunes(Span<char> span, List<Rune> runes)
    {
        int charsConsumed;
        for (var i = 0; i < span.Length; i += charsConsumed)
        {
            Rune.DecodeFromUtf16(span[i..], out Rune rune, out charsConsumed);
            runes.Add(rune);
        }
    }

    internal static void EventsIllegalTagException(string tag, int eventLineNumber, int eventFirstLineNumber)
    {
        throw new Exception($"Total line {eventLineNumber} / Event line {eventLineNumber - eventFirstLineNumber + 1} find illegal tag {tag}.");
    }

}