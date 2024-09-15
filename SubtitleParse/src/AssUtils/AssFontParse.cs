﻿using System.Text;
using Mobsub.SubtitleParse.AssTypes;
using Microsoft.Extensions.Logging;

namespace Mobsub.SubtitleParse.AssUtils;

public class AssFontParse(AssData ass, ILogger? logger = null)
{
    public Dictionary<AssFontInfo, HashSet<Rune>> GetUsedFontInfosWithEncoding()
    {
        var atp = new AssTagParse2(ass.Styles, ass.ScriptInfo, logger);
        Dictionary<AssFontInfo, HashSet<Rune>> usedFontsAndGlyphs = [];
        
        foreach (var d in atp.ParseEvents(ass.Events))
        {
            foreach (var (k, v) in d)
            {
                var afs = GetAssFontInfo(k);
                if (!usedFontsAndGlyphs.TryAdd(afs, new HashSet<Rune>(v)))
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
}