using System.Diagnostics;
using System.Text;
using Mobsub.SubtitleParse.AssTypes;
using Microsoft.Extensions.Logging;
using ZLogger;

namespace Mobsub.SubtitleParse.AssUtils;

public class AssFontParse(List<AssEvent> events, List<AssStyle> styles, ILogger? logger = null)
{
    private Dictionary<AssFontInfo, List<Rune>> usedFontsAndGlyphs = [];
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
    private List<Rune> curRunes = [];

    
    public Dictionary<AssFontInfo, List<Rune>> GetUsedFontInfosWithEncoding()
    {
        if (usedFontsAndGlyphs.Count > 0) return usedFontsAndGlyphs;
        
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
                            curRunes.Add(new Rune(AssConstants.NBSP_Utf16));
                            RecordFontGlyphs();
                            break;
                    }
                }
                else
                {
                    DecodeCharsToRunes(slice);
                    RecordFontGlyphs();
                }
            }
        }
        return usedFontsAndGlyphs;
    }
    public Dictionary<AssFontInfo, List<Rune>> GetUsedFontInfos()
    {
        if (usedFontsAndGlyphs.Count == 0)
        {
            GetUsedFontInfosWithEncoding();
        }
        Dictionary<AssFontInfo, List<Rune>> result = [];
        foreach (var map in usedFontsAndGlyphs)
        {
            AssFontInfo newAFI;
            if (map.Key.Encoding == 1)
            {
                newAFI = map.Key;
            }
            else
            {
                newAFI = map.Key with { Encoding = 1 };
            }
            
            if (result.TryGetValue(newAFI, out var existingValue))
            {
                existingValue.AddRange(map.Value);
            }
            else
            {
                result[newAFI] = [..map.Value];
            }
        }
        return result;
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
    
    private void RecordFontGlyphs()
    {
        if (curInDrawing){ return; }
        var assFontInfo = new AssFontInfo()
        {
            Name = curFontname!,
            Weight = (int)curWeight!,
            Italic = (bool)curItalic!,
            Encoding = (int)curEncoding!,
        };
        
        if (!usedFontsAndGlyphs.TryGetValue(assFontInfo, out var _))
        {
            usedFontsAndGlyphs.Add(assFontInfo, []);
        }

        foreach (var r in curRunes.Where(r => !usedFontsAndGlyphs[assFontInfo].Contains(r)))
        {
            usedFontsAndGlyphs[assFontInfo].Add(r);
        }

        curRunes.Clear();
    }

    private void DecodeCharsToRunes(Span<char> span)
    {
        int charsConsumed;
        for (var i = 0; i < span.Length; i += charsConsumed)
        {
            Rune.DecodeFromUtf16(span[i..], out var rune, out charsConsumed);
            curRunes.Add(rune);
        }
    }

    internal static void EventsIllegalTagException(string tag, int eventLineNumber, int eventFirstLineNumber)
    {
        throw new Exception($"Total line {eventLineNumber} / Event line {eventLineNumber - eventFirstLineNumber + 1} find illegal tag {tag}.");
    }

}