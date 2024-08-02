using System.Diagnostics;
using System.Text;
using Mobsub.SubtitleParse.AssTypes;
using Microsoft.Extensions.Logging;
using ZLogger;

namespace Mobsub.SubtitleParse.AssUtils;

public class AssFontParse(List<AssEvent> events, List<AssStyle> styles, ILogger? logger = null)
{
    private Dictionary<string, List<Rune>> usedFontGlyphs = [];
    private readonly Dictionary<string, AssStyle> stylesDict = styles.GroupBy(item => item.Name).ToDictionary(group => group.Key, group => group.Last());
    private AssStyle? defaultStyle;
    
    private readonly int? firstLineNumber = events.Count > 0 ? events.First().lineNumber : null;
    private int? curLineNumber;
    
    private AssStyle? curStyle;
    private string? curFontname;
    private bool? curItalic;
    private int? curWeight;
    private int? curEncoding;
    private bool curInDrawing = false;
    
    /// <summary>
    /// Get ass font info as string and used glyphs from ass data.
    /// Need to parse info string based on font file. Ass font encoding should be ignore.
    /// </summary>
    /// <returns>key is a string order by "font_used_name,font_weight,font_italic,font_encoding" (1 = true, 0 = false), value is rune collection</returns>
    public Dictionary<string, List<Rune>> GetUsedFonts()
    {
        if (!stylesDict.TryGetValue("Default", out defaultStyle))
        {
            defaultStyle = new AssStyle().GetDefault();
        }

        foreach (var eventLine in events)
        {
            Debug.WriteLine(eventLine.lineNumber);
            if (!eventLine.IsDialogue) continue;
            curLineNumber = eventLine.lineNumber;
            curInDrawing = false;
            var text = eventLine.Text.ToArray().AsSpan();
            List<Rune> runes = [];

            if (curStyle is null || eventLine.Style != curStyle.Name)
            {
                curStyle = stylesDict.GetValueOrDefault(eventLine.Style, defaultStyle);
            }
            curFontname = curStyle.Fontname;
            curItalic = curStyle.Italic;
            curWeight = curStyle.Bold ? 1 : 0;
            curEncoding = curStyle.Encoding;

            foreach (var block in text)
            {
                var slice = block.AsSpan();
                if (slice.Length > 0 && slice[0] == '{' && slice[^1] == '}')
                {
                    if (slice.Length > 2)
                    {
                        GetOverrideBlockFont(slice);
                    }
                }
                else if (slice.Length == 2 && AssConstants.IsEventSpecialCharPair(slice))
                {
                    switch (slice[1])
                    {
                        case 'h':
                            runes.Add(new Rune(AssConstants.NBSP_Utf16));
                            RecordFontGlyphs(runes);
                            break;
                    }
                }
                else
                {
                    DecodeCharsToRunes(slice, runes);
                    RecordFontGlyphs(runes);
                }
            }
        }
        return usedFontGlyphs;
    }
    public Dictionary<AssFontInfo, List<Rune>> GetUsedFontInfos()
    {
        GetUsedFonts();
        //return maps.ToDictionary(map => ParseAssFontInfo(map.Key), map => map.Value);
        Dictionary<AssFontInfo, List<Rune>> result = [];
        foreach (var map in usedFontGlyphs)
        {
            var k = ParseAssFontInfo(map.Key);
            var v = map.Value;
            if (result.TryAdd(k, v)) continue;
            foreach (var c in v)
            {
                if (!result[k].Contains(c))
                {
                    result[k].Add(c);
                }
            }
        }
        return result;
    }
    private static AssFontInfo ParseAssFontInfo(string fontInfoString)
    {
        var span = fontInfoString.AsSpan();
        var info = new AssFontInfo();

        var index = span.IndexOf(',');
        info.Name = span.Slice(0, index).ToString();

        span = span.Slice(index + 1);
        index = span.IndexOf(',');
        info.Weight = int.Parse(span.Slice(0, index));

        span = span.Slice(index + 1);
        index = span.IndexOf(',');
        info.Italic = int.Parse(span.Slice(0, index)) != 0;

        return info;
    }

    /// <summary>
    /// Parse font info from override tags (ovr block)
    /// </summary>
    private void GetOverrideBlockFont(Span<char> block)
    {
        var tags = AssTagParse.GetTagsFromOvrBlock(block, out var warningTags).ToArray().AsSpan();

        if (warningTags is not null)
        {
            logger?.ZLogWarning($"Event line {curLineNumber} ({curLineNumber - firstLineNumber}): {warningTags}");
        }
        foreach (var tag in tags)
        {
            GetOverrideFont(tag.AsSpan());
        }
    }
    
    /// <summary>
    /// Parse font info from override tag (start without backslash)
    /// </summary>
    private void GetOverrideFont(Span<char> tag)
    {
        var len = tag.Length;
        if (tag.StartsWith("fn"))
        {
            curFontname = len == 2 ? curStyle!.Fontname : tag[2..].ToString();
        }
        else if (tag.StartsWith("fe"))
        {
            curEncoding = len == 2 ? curStyle!.Encoding : int.Parse(tag[2..]);
        }
        else switch (tag[0])
        {
            case 'i' when tag.StartsWith("iclip"):
                break;
            case 'i':
                curItalic = len == 1 ? curStyle!.Italic : int.Parse(tag[1..]) == 1;
                break;
            
            case 'r':
            {
                if (tag.StartsWith("rnd"))
                {
                    // only vsfmod support
                }

                if (len > 1)
                {
                    curStyle = stylesDict!.GetValueOrDefault(tag[1..].ToString(), defaultStyle);
                }
                else
                {
                    curFontname = curStyle!.Fontname;
                    curItalic = curStyle.Italic;
                    curWeight = curStyle.Bold ? 1 : 0;
                    curEncoding = curStyle.Encoding;
                }

                break;
            }
            
            case 'b' when (len == 1 || len > 1 && tag[1] is not ('o' or 'l' or 'e')):
                curWeight = len == 1 ? (curStyle!.Bold ? 1 : 0) : int.Parse(tag[1..]);
                break;
            
            case 'p' when (len == 1 || len > 1 && tag[1] is not ('o' or 'b')):
                curInDrawing = len != 1 && int.Parse(tag[1..]) != 0;
                break;
        }
    }
    
    private void RecordFontGlyphs(List<Rune> runes)
    {
        if (curInDrawing){ return; }
        var fontStr = new string($"{curFontname},{curWeight},{((bool)curItalic! ? 1 : 0)},{curEncoding}");
        
        if (!usedFontGlyphs.TryGetValue(fontStr, out var _))
        {
            usedFontGlyphs.Add(fontStr, []);
        }

        foreach (var r in runes.Where(r => !usedFontGlyphs[fontStr].Contains(r)))
        {
            usedFontGlyphs[fontStr].Add(r);
        }

        runes.Clear();
    }

    private static void DecodeCharsToRunes(Span<char> span, List<Rune> runes)
    {
        int charsConsumed;
        for (var i = 0; i < span.Length; i += charsConsumed)
        {
            Rune.DecodeFromUtf16(span[i..], out var rune, out charsConsumed);
            runes.Add(rune);
        }
    }

    internal static void EventsIllegalTagException(string tag, int eventLineNumber, int eventFirstLineNumber)
    {
        throw new Exception($"Total line {eventLineNumber} / Event line {eventLineNumber - eventFirstLineNumber + 1} find illegal tag {tag}.");
    }

}