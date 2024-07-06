using System.Text;
using Microsoft.Extensions.Logging;
using ZLogger;
using Mobsub.SubtitleParse.AssTypes;

namespace Mobsub.SubtitleParse;

public class AssTagParse
{
    /// <summary>
    /// Extract override tags from event line Text, classified into 3 types: mod, weird and normal.
    /// </summary>
    /// <param name="et">event line text</param>
    /// <param name="modTags">not defined in spec, maybe VSFilterMod tags</param>
    /// <param name="weirdTags">normal tag but no value, should be pay attention</param>
    /// <param name="normalTags">normal tags which defined in spec or mainstream subtitle render</param>
    public static void ClassifyTagsFromLine(List<char[]> et, out StringBuilder modTags, out StringBuilder weirdTags, out StringBuilder normalTags, ILogger<AssTagParse>? logger = null)
    {
        modTags = new StringBuilder();
        weirdTags = new StringBuilder();
        normalTags = new StringBuilder();
        
        var text = et.ToArray();
        int step;
        for (var i = 0; i < text.Length; i += step)
        {
            step = 1;
            var slice = text[i].AsSpan();
            if (IsOverrideBlock(slice) && slice.Length > 2)
            {
                foreach (var ca in GetTagsFromOvrBlock(slice))
                {
                    ClassifyTagsFromBlock(ca.AsSpan(), modTags, weirdTags, normalTags, logger);
                }
            }
        }
    }

    private static void ClassifyTagsFromBlock(Span<char> tag, StringBuilder modTags, StringBuilder weirdTags, StringBuilder normalTags, ILogger<AssTagParse>? logger = null)
    {
        var len = tag.Length;

        if (len == 0)
        {
            logger?.ZLogError($"Tag is empty because consecutive backslash or bracket");
            return;
        }

        switch (tag[0])
        {
            case 'b':   // b, bord, be, blur
                if (len > 1)
                {
                    if (char.IsDigit(tag[1]))
                    {
                        RecordTag(tag, normalTags);
                    }
                    else if (tag[1].Equals('o'))
                    {
                        if (len > 3 && tag[2].Equals('r') && tag[3].Equals('d'))
                        {
                            if (len == 4)
                            {
                                RecordTag(tag, weirdTags);
                            }
                            else
                            {
                                RecordTag(tag, char.IsDigit(tag[4]) ? normalTags : modTags);
                            }
                        }
                        else
                        {
                            RecordTag(tag, modTags);
                        }
                    }
                    else if (tag[1].Equals('e'))
                    {
                        if (len == 2)
                        {
                            RecordTag(tag, weirdTags);
                        }
                        else
                        {
                            if (char.IsDigit(tag[2]))
                            {
                                RecordTag(tag, normalTags);
                            }
                            else
                            {
                                RecordTag(tag, modTags);
                            }
                        }
                    }
                    else if (tag[1].Equals('l'))
                    {
                        if (len > 3 && tag[2].Equals('u') && tag[3].Equals('r'))
                        {
                            if (len == 4)
                            {
                                RecordTag(tag, weirdTags);
                            }
                            else
                            {
                                if (char.IsDigit(tag[4]))
                                {
                                    RecordTag(tag, normalTags);
                                }
                                else
                                {
                                    RecordTag(tag, modTags);
                                }
                            }
                        }
                        else
                        {
                            RecordTag(tag, modTags);
                        }
                    }
                    else
                    {
                        RecordTag(tag, modTags);
                    }
                }
                else
                {
                    RecordTag(tag, weirdTags);
                }
                break;
            case 'u':   // u
            case 'K':   // K
            case 'q':   // q
                if (len > 1)
                {
                    if (char.IsDigit(tag[1]))
                    {
                        RecordTag(tag, normalTags);
                    }
                    else
                    {
                        RecordTag(tag, modTags);
                    }
                }
                else
                {
                    RecordTag(tag, weirdTags);
                }
                break;
            case 's':   // s, shad
                if (len > 1)
                {
                    if (char.IsDigit(tag[1]))
                    {
                        RecordTag(tag, normalTags);
                    }
                    else
                    {
                        if (len > 3 && tag[1] == 'h' && tag[2] == 'a' && tag[3] == 'd')
                        {
                            if (len == 4)
                            {
                                RecordTag(tag, weirdTags);
                            }
                            else
                            {
                                if (char.IsDigit(tag[4]))
                                {
                                    RecordTag(tag, normalTags);
                                }
                                else
                                {
                                    RecordTag(tag, modTags);
                                }
                            }
                        }
                    }
                }
                else
                {
                    RecordTag(tag, weirdTags);
                }
                break;
            case 'f':   // fn, fs, fscx, fscy, fsp, frx, fry, frz / fr, fe, fad, fade, fax, fay
                if (len > 1)
                {
                    if (len == 2)
                    {
                        if (tag[1] is 'n' or 's' or 'r' or 'e')
                        {
                            RecordTag(tag, weirdTags);
                        }
                        else
                        {
                            RecordTag(tag, modTags);
                        }
                    }
                    else
                    {
                        if (tag[1] == 'n')
                        {
                            RecordTag(tag, normalTags);
                        }
                        else if (tag[1] == 's')
                        {
                            if (char.IsDigit(tag[2]))
                            {
                                RecordTag(tag, normalTags);
                            }
                            else if (tag[2] == 'p')
                            {
                                if (len == 3)
                                {
                                    RecordTag(tag, weirdTags);
                                }
                                else if (len > 3 && char.IsDigit(tag[3]))
                                {
                                    RecordTag(tag, normalTags);
                                }
                                else if (len > 4 && tag[3] == '-' && char.IsDigit(tag[4]))
                                {
                                    RecordTag(tag, normalTags);
                                }
                                else
                                {
                                    RecordTag(tag, modTags);
                                }
                            }
                            else if (len > 3 && tag[2] == 'c' && (tag[3] is 'x' or 'y'))
                            {
                                if (len == 4)
                                {
                                    RecordTag(tag, weirdTags);
                                }
                                else
                                {
                                    if (tag[4] == '-' || char.IsDigit(tag[4]))
                                    {
                                        RecordTag(tag, normalTags);
                                    }
                                    else
                                    {
                                        RecordTag(tag, modTags);
                                    }
                                }
                            }
                            else
                            {
                                RecordTag(tag, modTags);
                            }
                        }
                        else if (tag[1] == 'r')
                        {
                            if (tag[2] is 'x' or 'y' or 'z')
                            {
                                if (len == 3)
                                {
                                    RecordTag(tag, weirdTags);
                                }
                                else if (len > 3 && char.IsDigit(tag[3]))
                                {

                                }
                                else if (len > 4 && tag[3] == '-' && char.IsDigit(tag[4]))
                                {

                                }
                                else
                                {
                                    RecordTag(tag, modTags);
                                }
                            }
                            else
                            {
                                RecordTag(tag, modTags);
                            }
                        }
                        else if (tag[1] == 'e')
                        {
                            if (char.IsDigit(tag[2]))
                            {

                            }
                            else
                            {
                                RecordTag(tag, modTags);
                            }
                        }
                        else if (tag[1] == 'a')
                        {
                            if (tag[2] == 'd')
                            {
                                if (len == 3)
                                {
                                    RecordTag(tag, weirdTags);
                                }
                                else
                                {
                                    if (tag[3] == '(' && char.IsDigit(tag[4]))
                                    {

                                    }
                                    else if (tag[3] == 'e')
                                    {
                                        if (len == 4)
                                        {
                                            RecordTag(tag, weirdTags);
                                        }
                                        else if (tag[4] == '(' && char.IsDigit(tag[5]))
                                        {

                                        }
                                        else
                                        {
                                            RecordTag(tag, modTags);
                                        }
                                    }
                                }
                            }
                            else if (tag[2] is 'x' or 'y')
                            {
                                if (len == 3)
                                {
                                    RecordTag(tag, weirdTags);
                                }
                                else if (tag[3] == '-' || char.IsDigit(tag[3]))
                                {

                                }
                                else
                                {
                                    RecordTag(tag, modTags);
                                }
                            }
                            else
                            {
                                RecordTag(tag, modTags);
                            }
                        }
                        else
                        {
                            RecordTag(tag, modTags);
                        }
                    }
                }
                else
                {
                    RecordTag(tag, modTags);
                }
                break;
            case 'c':   // c, clip
                if (len == 1)
                {
                    RecordTag(tag, weirdTags);
                }
                else if (len > 3 && tag[1] == 'l' && tag[2] == 'i' && tag[3] == 'p')
                {
                    if (len == 4)
                    {
                        RecordTag(tag, weirdTags);
                    }
                    else
                    {
                        if (tag[4] == '(')
                        {
                            RecordTag(tag, normalTags);
                        }
                        else
                        {
                            RecordTag(tag, modTags);
                        }
                    }
                }
                else
                {
                    if (len > 2 && tag[1] == '&' && tag[2] == 'H' && tag[^1] == '&')
                    {
                        RecordTag(tag, normalTags);
                    }
                    else
                    {
                        RecordTag(tag, weirdTags);
                    }
                }
                break;
            case 'i':   // i, iclip
                if (len == 1)
                {
                    RecordTag(tag, weirdTags);
                }
                else if (char.IsDigit(tag[1]))
                {
                    RecordTag(tag, normalTags);
                }
                else if (len > 4 && tag[1] == 'c' && tag[2] == 'l' && tag[3] == 'i' && tag[4] == 'p')
                {
                    if (len == 5)
                    {
                        RecordTag(tag, weirdTags);
                    }
                    else if (len > 5 && tag[5] == '(')
                    {
                        RecordTag(tag, normalTags);
                    }
                    else
                    {
                        RecordTag(tag, modTags);
                    }
                }
                else
                {
                    RecordTag(tag, modTags);
                }
                break;
            case '1':   // 1c, 1a
            case '2':   // 2c, 2a
            case '3':   // 3c, 3a
            case '4':   // 4c, 4a
                if (len == 1)
                {
                    RecordTag(tag, weirdTags);
                }
                else if (len > 1 && tag[1] is 'c' or 'a')
                {
                    if (len == 2)
                    {
                        RecordTag(tag, weirdTags);
                    }
                    else if (len > 3 && tag[2] == '&' && tag[3] == 'H')
                    {
                        RecordTag(tag, normalTags);
                    }
                    else
                    {
                        RecordTag(tag, weirdTags);
                    }
                }
                else
                {
                    RecordTag(tag, modTags);
                }
                break;
            case 'a':   // a, an, alpha
                if (len == 1)
                {
                    RecordTag(tag, weirdTags);
                }
                else if (len > 1 && tag[1] == 'n')
                {
                    if (len == 2)
                    {
                        RecordTag(tag, weirdTags);
                    }
                    else if (char.IsDigit(tag[2]))
                    {
                        RecordTag(tag, normalTags);
                    }
                    else
                    {
                        RecordTag(tag, modTags);
                    }
                }
                else if (len > 4 && tag[1] == 'l' && tag[2] == 'p' && tag[3] == 'h' && tag[4] == 'a')
                {
                    if (len == 5)
                    {
                        RecordTag(tag, weirdTags);
                    }
                    else if (tag[5] == '&')
                    {
                        RecordTag(tag, normalTags);
                    }
                    else
                    {
                        RecordTag(tag, weirdTags);
                    }
                }
                else
                {
                    RecordTag(tag, modTags);
                }
                break;
            case 'k':   // k, kf, ko, kt
                if (len == 1)
                {
                    RecordTag(tag, weirdTags);
                }
                else if (len > 1 && char.IsDigit(tag[1]))
                {
                    RecordTag(tag, normalTags);
                }
                else if (tag[1] == 'f')
                {
                    if (len > 2)
                    {
                        RecordTag(tag, modTags);
                    }
                }
                else if (tag[1] is 'o' or 't')
                {
                    if (len == 2)
                    {
                        RecordTag(tag, weirdTags);
                    }
                    else if (char.IsDigit(tag[2]))
                    {
                        RecordTag(tag, normalTags);
                    }
                    else
                    {
                        RecordTag(tag, modTags);
                    }
                }
                else
                {
                    RecordTag(tag, modTags);
                }
                break;
            case 'r':   // r, rStyle
                if (len > 2 && tag[1] == 'n' && tag[2] == 'd')
                {
                    if ((len == 3) || (tag[3] is 'x' or 'y' or 'z' or 's'))
                    {
                        RecordTag(tag, modTags);
                    }
                }
                else
                {
                    RecordTag(tag, normalTags);
                }
                break;
            case 't':   // t
                if (tag[^1] == ')')
                {
                    foreach (var ca in GetTagsFromTransFunction(tag))
                    {
                        ClassifyTagsFromBlock(ca, modTags, weirdTags, normalTags, logger);
                    }
                }
                else
                {
                    RecordTag(tag, weirdTags);
                }
                break;
            case 'm':   // move
                if (len > 3 && tag[1] == 'o' && tag[2] == 'v' && tag[3] == 'e')
                {
                    if (len == 4)
                    {
                        RecordTag(tag, weirdTags);
                    }
                    else if (tag[4] == '(')
                    {
                        RecordTag(tag, normalTags);
                    }
                    else
                    {
                        RecordTag(tag, modTags);
                    }
                }
                else
                {
                    RecordTag(tag, modTags);
                }
                break;
            case 'p':   // p, pos, pbo
                if (len == 1)
                {
                    RecordTag(tag, weirdTags);
                }
                else if (len > 2)
                {
                    if (tag[1] == 'o' && tag[2] == 's')
                    {
                        if (len == 3)
                        {
                            RecordTag(tag, weirdTags);
                        }
                        else if (len > 4 && tag[3] == '(')
                        {
                            RecordTag(tag, normalTags);
                        }
                        else
                        {
                            RecordTag(tag, modTags);
                        }
                    }
                    else if (tag[1] == 'b' && tag[2] == 'o')
                    {
                        if (len == 3)
                        {
                            RecordTag(tag, weirdTags);
                        }
                        else if (len > 4 && (tag[3] == '-' || char.IsDigit(tag[3])))
                        {
                            RecordTag(tag, normalTags);
                        }
                        else
                        {
                            RecordTag(tag, modTags);
                        }
                    }
                    else
                    {
                        RecordTag(tag, modTags);
                    }
                }
                break;
            case 'o':   // org
                if (len > 2 && tag[1] == 'r' && tag[2] == 'g')
                {
                    if (len == 3)
                    {
                        RecordTag(tag, weirdTags);
                    }
                    else if (tag[3] == '(')
                    {
                        RecordTag(tag, normalTags);
                    }
                    else
                    {
                        RecordTag(tag, modTags);
                    }
                }
                else
                {
                    RecordTag(tag, modTags);
                }
                break;
            case 'x':   // xbord, xshad
            case 'y':   // ybord, yshad
                if (len > 4 && ((tag[1] == 'b' && tag[2] == 'o' && tag[3] == 'r') || (tag[1] == 's' && tag[2] == 'h' && tag[3] == 'a')) && tag[4] == 'd')
                {
                    if (len == 5)
                    {
                        RecordTag(tag, weirdTags);
                    }
                    else if (char.IsDigit(tag[5]) || tag[5] == '-')
                    {
                        RecordTag(tag, normalTags);
                    }
                    else
                    {
                        RecordTag(tag, modTags);
                    }
                }
                else
                {
                    RecordTag(tag, modTags);
                }
                break;
            case '-':   // such as \-1
                RecordTag(tag, weirdTags);
                break;
            default:    // will miss digit tag, such as \666
                foreach (var c in tag)
                {
                    if (!char.IsDigit(c))
                    {
                        RecordTag(tag, modTags);
                        break;
                    }
                }
                break;
        }
    }

    private static void RecordTag(Span<char> tag, StringBuilder tags)
    {
        if (tags.Length > 0)
        {
            tags.Append(':');
        }
        tags.Append(tag);
    }

    /// <summary>
    /// Parse tag without backslash from event override tags block. Maybe record tags block which only have backslash.
    /// </summary>
    /// <param name="block">override tags block</param>
    /// <returns></returns>
    public static List<char[]> GetTagsFromOvrBlock(Span<char> block)
    {
        List<char[]> cal = [];

        if (IsOverrideBlock(block) && block.Length > 2)
        {
            block = block[1..^1];
        }

        var valueStartIndex = block.IndexOf(AssConstants.StartValueBlock);
        var preEndIndex = -1;

        if (valueStartIndex == -1)
        {
            // No functions
            SplitOnlyOvrTags(block, cal);
        }

        while (valueStartIndex != -1)
        {
            // parse tags between the previous function
            var preOvr = block[..valueStartIndex].LastIndexOf(AssConstants.BackSlash);
            if (preOvr == -1)
            {
                throw new InvalidDataException($"Invalid override block: {block.ToString()}");
            }
            
            if (preOvr != 0)
            {
                SplitOnlyOvrTags(block[(preEndIndex + 1)..preOvr], cal);
            }

            // function endwith )
            var valueEndIndex = block[valueStartIndex..].IndexOf(AssConstants.EndValueBlock);

            if (valueEndIndex != -1)
            {
                // get function value block
                GetTagsFromFunction(block.Slice(preOvr + 1, valueStartIndex + valueEndIndex - preOvr), cal, out var function);

                preEndIndex = valueStartIndex + valueEndIndex;
                valueStartIndex = block[preEndIndex..].IndexOf(AssConstants.StartValueBlock);
                if (valueStartIndex != -1)
                {
                    valueStartIndex += preEndIndex;
                }
                else
                {
                    preOvr = preEndIndex;
                }
            }
            
            if (valueStartIndex == -1)
            {
                SplitOnlyOvrTags(block[(preOvr + 1)..], cal);
                valueStartIndex = -1;
            }
        }
        return cal;
    }

    /// <summary>
    /// split override tags, maybe return empty when have consecutive backslash or bracket (maybe a bug, need fix)
    /// </summary>
    /// <param name="block"></param>
    /// <param name="cal"></param>
    /// <param name="ingoreConsecutive"></param>
    private static void SplitOnlyOvrTags(Span<char> block, List<char[]> cal, bool ingoreConsecutive)
    {
        if (block.IsEmpty)
        {
            return ;
        }
        var startIndex = block.IndexOf(AssConstants.BackSlash);
        Span<char> slice = default;

        while (startIndex != -1)
        {
            int endIndex = block[(startIndex + 1)..].IndexOf(AssConstants.BackSlash);
            if (endIndex != -1)
            {
                slice = block.Slice(startIndex + 1, endIndex);
                startIndex += endIndex + 1;
            }
            else
            {
                slice = block[(startIndex + 1)..];
                break;
            }
            
            if (ingoreConsecutive)
            {
                if (slice.Length > 0)
                {
                    cal.Add(slice.ToArray());
                }
            }
        }

        if (ingoreConsecutive)
        {
            if (slice.Length > 0)
            {
                cal.Add(slice.ToArray());
            }
        }
    }
    private static void SplitOnlyOvrTags(Span<char> block, List<char[]> cal) => SplitOnlyOvrTags(block, cal, true);

    private static void GetTagsFromFunction(Span<char> block, List<char[]> cal, out Span<char> function)
    {
        var openFunc = block.IndexOf(AssConstants.StartValueBlock);
        function = block[..openFunc];
        var closeFine = block.EndsWith(")".AsSpan());
        var startIndex = block.IndexOf(AssConstants.BackSlash);
        if (startIndex != -1)
        {
            var slice = closeFine ? block[startIndex..^1].Trim() : block[startIndex..].Trim();
            SplitOnlyOvrTags(slice, cal);
        }
        else
        {
            cal.Add(block.ToArray());
        }
    }

    public static List<char[]> GetTagsFromTransFunction(Span<char> block)
    {
        List<char[]> cal = [];
        GetTagsFromFunction(block, cal, out Span<char> function);
        if (function.Length != 1 && function[0] != 't')
        {
            throw new Exception($"Invalid transformation function: {block.ToString()}");
        }
        return cal;
    }

    public static bool IsOverrideBlock(Span<char> block)
    {
        if (block.Length < 2)
        {
            return false;
        }
        return block[0] == AssConstants.StartOvrBlock && block[^1] == AssConstants.EndOvrBlock;
    }

    public static bool IsTextBlock(Span<char> block) => !(IsOverrideBlock(block) || AssConstants.IsEventSpecialCharPair(block));

}