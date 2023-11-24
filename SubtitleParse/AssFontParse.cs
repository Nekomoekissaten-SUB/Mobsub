using System.Text;
using Mobsub.AssTypes;

namespace Mobsub.SubtitleParse;

public class AssFontParse
{
    public static void GetUsedFonts(List<AssEvent> events, List<AssStyle> styles, out Dictionary<string, List<char>> usedFontGlyphs)
    {
        usedFontGlyphs = new Dictionary<string, List<char>>{};
        var lineNumberFirst = events.First().lineNumber;
        
        foreach (var eventLine in events)
        {
            if (eventLine.IsDialogue)
            {
                var eventStyle = GetStyleByName(styles, eventLine.Style);
                var textSpan = eventLine.Text.AsSpan();
                var lineNumber = eventLine.lineNumber;

                var fn = new StringBuilder().Append(eventStyle.Fontname);
                var fe = new StringBuilder().Append(eventStyle.Encoding);
                var italic = new StringBuilder().Append(eventStyle.Italic ? '1' : '0');
                var weight = new StringBuilder().Append(eventStyle.Bold);

                var ovr = false;
                // var parentheses = false;
                var tpass = false;
                var maybeWrap = false;
                var str = new List<char>(){};
                bool newfont;

                for (var i = 0; i < textSpan.Length; i++)
                {
                    var s = textSpan[i];

                    switch (s)
                    {
                        case '{':
                            ovr = true;
                            if (str.Count > 0)
                            {
                                RecordFontGlyphs(fn, fe, italic, weight, str, usedFontGlyphs);
                                newfont = false;
                                str.Clear();
                            }
                            break;
                        case '}':
                            if (str.Count > 0)
                            {
                                GetOvrideFont(str, eventStyle, styles, fn, fe, italic, weight, out newfont, lineNumber, lineNumberFirst);
                            }
                            ovr = false;
                            str.Clear();
                            break;
                        // case '(':
                            // if (ovr)
                            // {
                            //     parentheses = true;
                            // }
                        //     str.Add(s);
                        //     break;
                        // case ')':
                            // if (ovr)
                            // {
                            //     parentheses = false;
                            // }
                            // str.Add(s);
                            // break;
                        case '\\':
                            tpass = false;
                            // if (ovr)
                            // {
                            //     if (parentheses)
                            //     {
                            //         str.Add(s);
                            //     }
                            //     else
                            //     {
                            //         if (str.Count > 0)
                            //         {
                            //             GetOvrideFont(str, eventStyle, styles, fn, fe, italic, weight, out newfont, lineNumber, lineNumberFirst);
                            //             str.Clear();
                            //         }
                            //     }
                            // }
                            if (ovr)
                            {
                                if (str.Count > 0)
                                {
                                    GetOvrideFont(str, eventStyle, styles, fn, fe, italic, weight, out newfont, lineNumber, lineNumberFirst);
                                    str.Clear();
                                }
                            }
                            else
                            {
                                maybeWrap = true;
                            }
                            break;
                        case 'N':
                        case 'n':
                        case 'h':
                            if (!maybeWrap)
                            {
                                str.Add(s);
                            }
                            else
                            {
                                maybeWrap = false;
                            }
                            break;

                        default:

                            if (str.Count == 2 && str[0] == 't' && str[1] == '(')
                            {
                                tpass = true;
                                str.Clear();
                            }
                            if (!tpass)
                            {
                                str.Add(s);
                            }
                            break;
                    }
                }

                RecordFontGlyphs(fn, fe, italic, weight, str, usedFontGlyphs);
                newfont = false;
                str.Clear();
            }

        }
    }

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
                return AssStyle.Fallback();
            }
        }
    }

    private static void GetOvrideFont(List<char> tag, AssStyle eventStyle, List<AssStyle> styles, StringBuilder fn, StringBuilder fe, StringBuilder italic, StringBuilder weight, out bool newfont, int lineNumber, int lineNumberFirst)
    {
        var len = tag.Count;
        newfont = false;

        if (tag[0].Equals('f'))
        {
            if (tag[1] == 'n')
            {
                fn.Clear();
                foreach (var c in tag[2..len])
                {
                    fn.Append(c);
                }
                newfont = true;
            }
            // \fe should be ignore
            else if (tag[1] == 'e')
            {
                fe.Clear();
                foreach (var c in tag[2..len])
                {
                    fe.Append(c);
                }
                newfont = true;
            }
        }
        else if (tag[0] == 'i')
        {
            italic.Clear().Append(tag.Count > 1 ? tag[1] : '0');
            newfont = true;
        }
        else if (tag[0] == 'r')
        {
            if (len > 1)
            {
                var af = GetStyleByName(styles, string.Concat(tag[1..len]));
                fn.Clear().Append(af.Fontname);
                weight.Clear().Append(af.Bold);
                italic.Clear().Append(af.Italic ? '1' : '0');
                fe.Clear().Append(af.Encoding);
                newfont = true;
            }
            else
            {
                fn.Clear().Append(eventStyle.Fontname);
                weight.Clear().Append(eventStyle.Bold);
                italic.Clear().Append(eventStyle.Italic ? '1' : '0');
                fe.Clear().Append(eventStyle.Encoding);
                newfont = true;
            }
        }
        else if (tag[0] == 'b')
        {
            if (tag.Count > 1 && tag[1] is 'o' or 'l' or 'e')
            {
            }
            else
            {
                weight.Clear();
                if (tag.Count > 1)
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
                                EventsIllegalTagException(string.Concat(tag), lineNumber, lineNumberFirst);
                            }
                        }
                    }
                    else
                    {
                        EventsIllegalTagException(string.Concat(tag), lineNumber, lineNumberFirst);
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
                newfont = true;
            }
        }
    }

    private static void RecordFontGlyphs(StringBuilder fn, StringBuilder fe, StringBuilder italic, StringBuilder weight, List<char> str, Dictionary<string, List<char>> usedFontGlyphs)
    {
        var fontStr = new StringBuilder().Append(fn).Append(',').Append(weight).Append(',').Append(italic).Append(',').Append(fe).ToString();
        if (!usedFontGlyphs.TryGetValue(fontStr, out List<char>? _))
        {
            usedFontGlyphs.Add(fontStr, new List<char>{});
        }
    
        foreach (var c in str)
        {
            if (!usedFontGlyphs[fontStr].Contains(c))
            {
                usedFontGlyphs[fontStr].Add(c);
            }
        }
    }

    internal static void EventsIllegalTagException(string tag, int eventLineNumber, int eventFirstLineNumber)
    {
        throw new Exception($"Total line {eventLineNumber} / Event line {eventLineNumber - eventFirstLineNumber + 1} find illegal tag {tag}.");
    }

}