using System.Text;
using Mobsub.SubtitleParse.AssTypes;
using Microsoft.Extensions.Logging;

namespace Mobsub.SubtitleParse.AssUtils;

public class AssAnalyze(AssData ass, ILogger? logger = null)
{
    private AssTagParse? atp;
    private Dictionary<AssTextStyle, List<Rune>>[]? tagParseResult;
    
    public Dictionary<AssFontInfo, HashSet<Rune>> GetUsedFontInfosWithEncoding()
    {
        GenerateTagParseResult();
        Dictionary<AssFontInfo, HashSet<Rune>> usedFontsAndGlyphs = [];
        
        foreach (var d in tagParseResult!)
        {
            foreach (var (k, v) in d)
            {
                var afs = GetAssFontInfo(k);
                if (!usedFontsAndGlyphs.TryAdd(afs, [..v]))
                {
                    usedFontsAndGlyphs[afs].UnionWith(v);
                }
            }
        }

        return usedFontsAndGlyphs;
    }
    
    public Dictionary<AssFontInfo, HashSet<Rune>> GetUsedFontInfos()
    {
        var usedFontsAndGlyphs = GetUsedFontInfosWithEncoding();
        Dictionary<AssFontInfo, HashSet<Rune>> result = [];
        foreach (var (k, v) in usedFontsAndGlyphs)
        {
            AssFontInfo newAfi;
            if (k.Encoding == 1)
            {
                newAfi = k;
            }
            else
            {
                newAfi = k with { Encoding = 1 };
            }
            
            if (result.TryGetValue(newAfi, out var existingValue))
            {
                existingValue.UnionWith(v);
            }
            else
            {
                result[newAfi] = [..v];
            }
        }
        return result;
    }
    
    private static AssFontInfo GetAssFontInfo(AssTextStyle ts)
    {
        return new AssFontInfo()
        {
            Name = ts.FontName ?? ts.BaseStyle.Fontname,
            Weight = ts.FontWeight ?? (ts.BaseStyle.Bold ? 1 : 0),
            Italic = ts.FontItalic ?? ts.BaseStyle.Italic,
            Encoding = ts.FontEncoding ?? ts.BaseStyle.Encoding,
        };
    }

    public HashSet<string> GetUsedStyles()
    {
        var styles = ass.Events.Collection
            .Where(evt => evt.IsDialogue)
            .Select(evt => evt.Style)
            .ToHashSet();

        GenerateTagParseResult();
        styles.UnionWith(atp!.GetResetStyles());
        return styles;
    }
    public HashSet<string> GetUndefinedStyles()
    {
        var usedStyles = GetUsedStyles();
        var undefinedStyles = new HashSet<string>(usedStyles);
        undefinedStyles.ExceptWith(ass.Styles.Names);
        return undefinedStyles;
    }
    
    private void GenerateTagParseResult()
    {
        if (tagParseResult is not null){ return; }
        atp ??= new AssTagParse(ass.Styles, ass.ScriptInfo, logger);
        tagParseResult = atp.ParseEvents(ass.Events).ToArray();
    }
    
}