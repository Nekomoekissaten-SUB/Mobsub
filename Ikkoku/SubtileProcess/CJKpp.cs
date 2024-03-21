using System.Text;
using Mobsub.AssTypes;
using Mobsub.SubtitleParse;
using OpenCCSharp.Conversion;

namespace Mobsub.Ikkoku;

public partial class SubtileProcess
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
}