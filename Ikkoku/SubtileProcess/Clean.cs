using System.Diagnostics;
using System.Text;
using Mobsub.AssTypes;

namespace Mobsub.Ikkoku;

public partial class SubtileProcess
{
    public static void CleanAss( AssData data, bool keepComment, bool verbose, ReadOnlySpan<char> assFileName, bool addLayoutRes, bool dropUnusedStyles, out string msg, out bool untouched)
    {
        var records = new StringBuilder();

        HashSet<AssSection> rmSections = [
            AssSection.Fonts,
            AssSection.Graphics,
            AssSection.AegisubProjectGarbage,
            AssSection.AegisubExtradata
            ];

        // remove list sections
        if (verbose)
        {
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
        }
        data.Sections.ExceptWith(rmSections);

        // process script info
        if (verbose)
        {
            records.Append("Script Info:");
        }
        // remove comment lines
        if (!keepComment && (data.ScriptInfo.Comment.Count > 0))
        {
            data.ScriptInfo.Comment.Clear();
            if (verbose)
            {
                records.Append(" remove comments;");
            }
        }
        // change title to ass filename without suffix
        if (assFileName.Length > 0)
        {
            if (!data.ScriptInfo.Title.AsSpan().SequenceEqual(assFileName))
            {
                data.ScriptInfo.Title = assFileName.ToString();
                if (verbose)
                {
                    records.Append(" rename title;");
                }
            }
        }
        // add layoutres
        if (addLayoutRes)
        {
            if ((data.ScriptInfo.Orders.Add("LayoutResX") || data.ScriptInfo.Orders.Add("LayoutResY")) && verbose)
            {
                records.Append(" add LayoutResX/Y;");
            }
        }
        if (verbose)
        {
            RecordRemoveLast(records, 12);
        }
    
        // process events
        if (verbose)
        {
            records.Append("Events:");
        }
        // always record undefined styles
        var usedStyles = GetUsedStyles(data.Events.Collection);
        var undefinedStyles = new HashSet<string>(usedStyles);
        undefinedStyles.ExceptWith(data.Styles.Names);
        if (undefinedStyles.Count > 0)
        {
            if (verbose)
            {
                records.Append($" undefined styles {string.Join(", ", undefinedStyles)};");
            }
            else
            {
                records.AppendLine($"Events: undefined styles {string.Join(", ", undefinedStyles)}");
            }
        }
        // remove weird chars, replace weird space, remove aegisub-motion garbage
        var unusedChar = new char[] { '\u200E', '\u200F'};
        var weirdSpace = new char[] { '\u00a0' };
        var etsb = new StringBuilder();
        var et1Blk = false;
        var hadMotionGarbage = false;
        var hadUnusedChar = false;
        var hadWeridSpace = false;
        for (var i = 0; i < data.Events.Collection.Count; i++)
        {
            var et = data.Events.Collection[i].Text;
            etsb.Append(et);
            for (var j = 0; j < etsb.Length; j++)
            {
                var c = etsb[j];
                if (j == 0 && c == '{')
                {
                    et1Blk = true;
                }
                else if (et1Blk && c == '}' && etsb[1] == '=' && char.IsDigit(etsb[2]))
                {
                    etsb.Remove(0, j + 1);
                    j = 0;
                    et1Blk = false;
                    if (!hadMotionGarbage)
                    {
                        hadMotionGarbage = true;
                    }
                }
                else if (unusedChar.Contains(c))
                {
                    etsb.Remove(j, 1);
                    j--;
                    if (!hadUnusedChar)
                    {
                        hadUnusedChar = true;
                    }
                }
                else if (weirdSpace.Contains(c))
                {
                    etsb[j] = '\u0020';
                    if (!hadWeridSpace)
                    {
                        hadWeridSpace = true;
                    }
                }
            }
            if (!etsb.Equals(et.AsSpan()))
            {
                data.Events.Collection[i].Text = etsb.ToString();
            }
            etsb.Clear();
        }
        if (verbose)
        {
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
        }

        // process styles if drop unused styles
        if (dropUnusedStyles)
        {
            var unusedStyles = new HashSet<string>(data.Styles.Names);
            unusedStyles.ExceptWith(usedStyles);
            if (unusedStyles.Count > 0)
            {
                data.Styles.Names.ExceptWith(unusedStyles);
                data.Styles.Collection.RemoveAll(x => unusedStyles.Contains(x.Name));

                if (verbose)
                {
                    records.AppendLine($"Styles: remove unused styles {string.Join(", ", unusedStyles)}");
                }
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

    // public static string CleanAss( AssData data, bool record, string assFileName, bool addLayoutRes, bool dropUnusedStyles)
    // {
    //     return CleanAss( data, false, record, assFileName, addLayoutRes, dropUnusedStyles);
    // }

    // public static string CleanAss( AssData data, string assFileName)
    // {
    //     return CleanAss( data, false, true, assFileName, true, false);
    // }

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