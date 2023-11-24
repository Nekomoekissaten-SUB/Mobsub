using System.Text;
using Mobsub.AssTypes;

namespace Mobsub.SubtitleParse;

public class AssTagParse
{
    /// <summary>
    /// Extract override tags from event line text
    /// </summary>
    /// <param name="et">event line text</param>
    /// <param name="modTags">not defined in spec, maybe VSFilterMod tags</param>
    /// <param name="weirdTags">normal tag but no value, should be pay attention</param>
    /// <param name="normalTags">normal tags which defined in spec or mainstream subtitle render</param>
    public static void GetEventTags(ReadOnlySpan<char> et, out StringBuilder modTags, out StringBuilder weirdTags, out StringBuilder normalTags)
    {
        var ovr = false;
        var str = new List<char>(){};
        modTags = new StringBuilder();
        weirdTags = new StringBuilder();
        normalTags = new StringBuilder();
        var tTag = false;
        var record = false;
        
        for (var i = 0; i < et.Length; i++)
        {
            var s = et[i];
        
            switch (s)
            {
                case '{':
                    str.Clear();
                    ovr = true;
                    break;
                case '}':
                    if (ovr && str.Count > 0)
                    {
                        if (tTag && str.Last() == ')')
                        {
                            str.RemoveAt(str.Count - 1);
                        }
                        ExtractTag(str, modTags, weirdTags, normalTags);
                    }
                    str.Clear();
                    ovr = false;
                    record = false;
                    break;
                case '\\':
                    record = true;
                    if (ovr && str.Count > 0)
                    {
                        if (tTag && str.Last() == ')')
                        {
                            str.RemoveAt(str.Count - 1);
                        }
                        ExtractTag(str, modTags, weirdTags, normalTags);
                        str.Clear();
                    }
                    break;

                default:
                    if (str.Count == 2 && str[0] == 't' && str[1] == '(')
                    {
                        record = false;
                        tTag = true;
                        str.Clear();
                    }
                    if (record)
                    {
                        str.Add(s);
                    }
                    break;
            }
        }
        
        str.Clear();
    }

    private static void ExtractTag(List<char> tag, StringBuilder modTags, StringBuilder weirdTags, StringBuilder normalTags)
    {
        var len = tag.Count;

        switch (tag[0])
        {
            case 'b':   // b, bold, be, blur
                if (len > 1)
                {
                    if (char.IsDigit(tag[1]))
                    {
                        RecordTag(tag, normalTags);
                    }
                    else if (tag[1].Equals('o'))
                    {
                        if (len > 3 && tag[2].Equals('l') && tag[3].Equals('d'))
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
                    if (len > 2 && tag[1] == '&' && tag[2] == 'H' && tag.Last() == '&')
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
                    if ((len == 3) || (len > 3 && tag[3] is 'x' or 'y' or 'z' or 's'))
                    {
                        RecordTag(tag, modTags);
                    }
                }
                else
                {
                    RecordTag(tag, normalTags);
                }
                break;
            case 't':   // t    // not record t tags
                break;
            case 'm':   // move
                if (len > 3 && tag[1] == 'o' && tag[2] == 'v' && tag[3] == 'e')
                {
                    if (len == 4)
                    {
                        RecordTag(tag, weirdTags);
                    }
                    else if (len > 4 && tag[4] == '(')
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
                    else if (len > 3 && tag[3] == '(')
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
                    else if (len > 5 && (char.IsDigit(tag[5]) || tag[5] == '-'))
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

    private static void RecordTag(List<char> tag, StringBuilder tags)
    {
        if (tags.Length > 0)
        {
            tags.Append(':');
        }

        foreach (var c in tag)
        {
            tags.Append(c);
        }
    }


}