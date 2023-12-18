using System.Diagnostics;
using System.Text;
using Mobsub.AssTypes;
using Mobsub.SubtitleParse;

namespace Mobsub.Ikkoku;

public partial class SubtileProcess
{
    public static void CleanAss(AssData data, bool keepComment, ReadOnlySpan<char> assFileName, bool addLayoutRes, bool dropUnusedStyles, out string msg, out bool untouched)
    {
        var records = new StringBuilder();

        HashSet<AssSection> rmSections = [
            AssSection.Fonts,
            AssSection.Graphics,
            AssSection.AegisubProjectGarbage,
            AssSection.AegisubExtradata
            ];

        // remove list sections
        var removeRecords = new List<string>();
        foreach (var s1 in rmSections)
        {
            if (data.Sections.Contains(s1))
            {
                removeRecords.Add(
                    s1 switch
                    {
                        AssSection.Fonts => "Fonts",
                        AssSection.Graphics => "Graphics",
                        AssSection.AegisubProjectGarbage => "Aegisub Project Garbage",
                        AssSection.AegisubExtradata => "Aegisub Extradata",
                        _ => "Unknown Section"
                    }
                );
            }
        }
        if (removeRecords.Count > 0)
        {
            records.AppendLine($"Sections: remove {string.Join(", ", removeRecords)}");
        }

        data.Sections.ExceptWith(rmSections);

        // process script info
        records.Append("Script Info:");
        // remove comment lines
        if (!keepComment && (data.ScriptInfo.Comment.Count > 0))
        {
            data.ScriptInfo.Comment.Clear();
            records.Append(" remove comments;");
        }
        // change title to ass filename without suffix
        if (assFileName.Length > 0)
        {
            if (!data.ScriptInfo.Title.AsSpan().SequenceEqual(assFileName))
            {
                data.ScriptInfo.Title = assFileName.ToString();
                records.Append(" rename title;");
            }
        }
        // add layoutres
        if (addLayoutRes)
        {
            var _addLayoutX = data.ScriptInfo.Orders.Add("LayoutResX");
            var _addLayoutY = data.ScriptInfo.Orders.Add("LayoutResY");
            if (_addLayoutX || _addLayoutY)
            {
                records.Append(" add LayoutResX/Y;");
            }
        }
        RecordRemoveLast(records, 12);
    
        // process events
        records.Append("Events:");
        // always record undefined styles
        var usedStyles = GetUsedStyles(data.Events.Collection);
        var undefinedStyles = new HashSet<string>(usedStyles);
        undefinedStyles.ExceptWith(data.Styles.Names);
        if (undefinedStyles.Count > 0)
        {
            records.Append($" undefined styles {string.Join(", ", undefinedStyles)};");
        }
        // remove weird chars, replace weird space, remove aegisub-motion garbage
        var unusedChar = new char[] { '\u200E', '\u200F'};
        var weirdSpace = new char[] { '\u00a0' };
        var etsb = new StringBuilder();
        var hadMotionGarbage = false;
        var hadUnusedChar = false;
        var hadWeridSpace = false;
        
        for (var i = 0; i < data.Events.Collection.Count; i++)
        {
            var et = data.Events.Collection[i].Text;

            // {=} {=0} {=99}            
            if (et.Count > 0 && AssTagParse.IsOvrrideBlock(et[0].AsSpan()) && et[0][1] == '=' && ((et[0].Length > 3 && char.IsDigit(et[0][2])) || et[0].Length == 3))
            {
                et.RemoveAt(0);
                hadMotionGarbage = true;
            }

            var _mod = false;
            for (var j = 0; j < et.Count; j++)
            {
                var blk = et.ToArray()[j];
                foreach (var c in blk)
                {
                    if (unusedChar.Contains(c))
                    {
                        _mod = true;
                        if (!hadUnusedChar)
                        {
                            hadUnusedChar = true;
                        }
                    }
                    else if (weirdSpace.Contains(c))
                    {
                        _mod = true;
                        etsb.Append('\u0020');
                        if (!hadWeridSpace)
                        {
                            hadWeridSpace = true;
                        }
                    }
                    else
                    {
                        etsb.Append(c);
                    }
                }
                
                if (_mod)
                {
                    char[] _new = [];
                    etsb.CopyTo(0, _new, etsb.Length);
                    et[j] = _new;
                }
                etsb.Clear();
            }
        }

        if (hadMotionGarbage)
        {
            records.Append(" remove aegisub-motion garbage;");
        }
        if (hadUnusedChar)
        {
            records.Append(" remove unused unicode chars;");
        }
        if (hadWeridSpace)
        {
            records.Append(" replace weird space chars;");
        }
        RecordRemoveLast(records, 7);


        // process styles if drop unused styles
        if (dropUnusedStyles)
        {
            var unusedStyles = new HashSet<string>(data.Styles.Names);
            unusedStyles.ExceptWith(usedStyles);
            if (unusedStyles.Count > 0)
            {
                data.Styles.Names.ExceptWith(unusedStyles);
                data.Styles.Collection.RemoveAll(x => unusedStyles.Contains(x.Name));
                records.AppendLine($"Styles: remove unused styles {string.Join(", ", unusedStyles)}");
            }
        }

        // End
        untouched = false;
        if (records.Length == 0)
        {
            records.AppendLine("No changes");
            untouched = true;
        }
        msg = records.ToString();
    }

    private static void RecordRemoveLast( StringBuilder sb, int lineLength)
    {
        Debug.Assert(sb[^1] is ';' or ':');
        switch (sb[^1])
        {
            case ';':
                sb.Remove(sb.Length - 1, 1);
                sb.AppendLine();
                break;
            case ':':
                sb.Remove(sb.Length - lineLength, lineLength);
                break;
        }
    }

    public static void RemoveChar( StringBuilder sb, char[] chars)
    {
        for (int i = 0; i < sb.Length; i++)
        {
            foreach (var c in chars)
            {
                if (sb[i] == c)
                {
                    sb.Remove(i, 1);
                    i--;
                }
            }
        }
    }

    public static void RemoveChar( StringBuilder sb, char c)
    {
        for (int i = 0; i < sb.Length; i++)
        {
            if (sb[i] == c)
            {
                sb.Remove(i, 1);
                i--;
            }
        }
    }

}