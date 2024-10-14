using System.Text;
using Mobsub.SubtitleParse.AssTypes;
using Mobsub.SubtitleParse;
using OpenCCSharp.Conversion;

namespace Mobsub.Ikkoku.SubtileProcess;

public class CJKpp
{
    public static void ZhConvertEventLineByOpenccsharp(AssEvent evt, StringBuilder sb, ChainedScriptConverter converter, out string[]? charsCountChange)
    {
        // wip: correct block insert position
        sb.Clear();
        charsCountChange = null;

        var text = evt.Text.AsSpan();
        if (evt.TextRanges.Length == 0)
        {
            evt.UpdateTextRanges();
        }
        List<int> textBlockIndex = [];
        for (var i = 0; i < evt.TextRanges.Length; i++)
        {
            var range = evt.TextRanges[i];
            var blk = text[range];
            if (AssEvent.IsTextBlock(blk))
            {
                sb.Append(blk);
                textBlockIndex.Add(i);
            }
        }

        var sconvp = converter.Convert(sb.ToString()).AsSpan();

        if (sconvp.Length != sb.Length)
        {
            charsCountChange = [sb.ToString(), sconvp.ToString()];
        }
        sb.Clear();

        var offset = 0;
        for (var i = 0; i < evt.TextRanges.Length; i++)
        {
            var range = evt.TextRanges[i];
            if (!textBlockIndex.Contains(i))
            {
                sb.Append(text[range]);
                offset += range.End.Value - range.Start.Value;
                continue;
            }

            var offsetRange = new Range(range.Start.Value - offset, range.End.Value - offset);
            if (textBlockIndex.Last() == i)
            {
                sb.Append(sconvp[offsetRange.Start..]);
                continue;
            }

            sb.Append(sconvp[offsetRange]);
        }

        evt.Text = sb.ToString();
        evt.UpdateTextRanges();
    }
    
    public static bool NotZhConvert(AssEvent evt)
    {
        return evt.Style.AsSpan().Contains("JP".AsSpan(), StringComparison.OrdinalIgnoreCase);
    }

    internal static bool StyleZhConvert(AssEvent evt)
    {
        var span = evt.Style.AsSpan();
        var len = span.Length;

        if (len < 3) { return false; }

        var posChs = span.IndexOf("CHS".AsSpan(), StringComparison.OrdinalIgnoreCase);
        var posSc = span.IndexOf("SC".AsSpan(), StringComparison.OrdinalIgnoreCase);
        var posScr = span.IndexOf("SCR".AsSpan(), StringComparison.OrdinalIgnoreCase);

        if (posChs == -1 && posSc == -1) { return false; }
        var sb = new StringBuilder(len);

        if (posChs > -1)
        {
            sb.Append(span.Slice(0, posChs + 2));
            sb.Append(span[posChs + 2] == 'S' ? 'T' : 't');

            if (len != posChs + 3)
            {
                sb.Append(span[(posChs + 3)..]);
            }

            evt.Style = sb.ToString();
            return true;
        }
        
        if (posSc > -1 && posScr == -1)
        {
            sb.Append(span.Slice(0, posSc));
            sb.Append(span[posSc] == 'S' ? 'T' : 't');
            sb.Append(span[(posSc + 1)..]);

            evt.Style = sb.ToString();
            return true;
        }
        
        // screen, scr
        return false;
    }
}