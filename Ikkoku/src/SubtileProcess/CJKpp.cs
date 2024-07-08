using System.Text;
using Mobsub.SubtitleParse.AssTypes;
using Mobsub.SubtitleParse;
using OpenCCSharp.Conversion;

namespace Mobsub.Ikkoku.SubtileProcess;

public class CJKpp
{
    public static void ZhConvertEventLineByOpenccsharp(List<char[]> etText, StringBuilder sb, ChainedScriptConverter converter, out string[]? charsCountChange)
    {
        sb.Clear();
        charsCountChange = null;

        List<int> textBlockIndex = [];
        for (var i = 0; i < etText.Count; i++)
        {
            var blk = etText[i];
            if (AssTagParse.IsTextBlock(blk))
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

        var _start = 0;
        int blkLen;
        for (var i = 0; i < etText.Count; i++)
        {
            blkLen = etText[i].Length;
            if (textBlockIndex.Contains(i))
            {
                if (textBlockIndex.Last() == i)
                {
                    etText[i] = sconvp[_start..].ToArray();
                }
                else
                {
                    etText[i] = sconvp.Slice(_start, blkLen).ToArray();
                }

                _start += blkLen;
            }
        }
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
        else if (posSc > -1 && posScr == -1)
        {
            sb.Append(span.Slice(0, posSc));
            sb.Append(span[posSc] == 'S' ? 'T' : 't');
            sb.Append(span[(posSc + 1)..]);

            evt.Style = sb.ToString();
            return true;
        }
        else
        {
            // screen, scr
            return false;
        }
    }
}