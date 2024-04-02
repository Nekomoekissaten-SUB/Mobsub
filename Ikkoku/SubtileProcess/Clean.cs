using System.Diagnostics;
using System.Text;
using Mobsub.SubtitleParse.AssTypes;

namespace Mobsub.Ikkoku.SubtileProcess;

public class Clean
{
    public struct CleanAssArgs
    {
        // Script Info
        internal bool keepComment;
        internal bool renameTitle;
        internal bool addLayoutRes;

        // Events
        internal bool dropUnusedStyles;
        internal bool processEvents;
        internal bool rmMotionGarbage;   // AegisubExtradata
    }

    public static void CleanAss(AssData data, ReadOnlySpan<char> assFileName, CleanAssArgs args, out string msg, out bool untouched)
    {
        var records = new StringBuilder();

        HashSet<AssSection> rmSections = [
            AssSection.Fonts,
            AssSection.Graphics,
            AssSection.AegisubProjectGarbage,
            AssSection.AegisubExtradata
            ];
        
        if (!args.rmMotionGarbage)
            rmSections.Remove(AssSection.AegisubExtradata);

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
        if (!args.keepComment && (data.ScriptInfo.Comment.Count > 0))
        {
            data.ScriptInfo.Comment.Clear();
            records.Append(" remove comments;");
        }
        // change title to ass filename without suffix
        if (args.renameTitle && assFileName.Length > 0)
        {
            if (!data.ScriptInfo.Title.AsSpan().SequenceEqual(assFileName))
            {
                data.ScriptInfo.Title = assFileName.ToString();
                data.ScriptInfo.Orders.Add("Title");
                records.Append(" rename title;");
            }
        }
        // add layoutres
        if (args.addLayoutRes)
        {
            var _addLayoutX = data.ScriptInfo.Orders.Add("LayoutResX");
            var _addLayoutY = data.ScriptInfo.Orders.Add("LayoutResY");
            if (_addLayoutX || _addLayoutY)
            {
                records.Append(" add LayoutResX/Y;");
            }
        }
        RecordRemoveLast(records, 12);
    
        if (args.processEvents || args.dropUnusedStyles)
        {
            var usedStyles = Check.GetUsedStyles(data.Events.Collection);
            // Default style always use
            usedStyles.Add("Default");

            // process events
            if (args.processEvents)
            {
                records.Append("Events:");
                // always record undefined styles
                var undefinedStyles = new HashSet<string>(usedStyles);
                undefinedStyles.ExceptWith(data.Styles.Names);
                if (undefinedStyles.Count > 0)
                {
                    records.Append($" undefined styles {string.Join(", ", undefinedStyles)};");
                }
                // remove weird chars, replace weird space, remove aegisub-motion garbage
                var etsb = new StringBuilder();
                var hadWeridTime = false;
                var weirdTimeEventLines = new List<int>();
                var eventLineFirst = data.Events.Collection[0].lineNumber;
                var hadMotionGarbage = false;
                var hadUnusedChar = false;
                var hadWeridSpace = false;
                var hadEndSpace = false;
                
                for (var i = 0; i < data.Events.Collection.Count; i++)
                {
                    if (Check.WeridTimeOneLine(data.Events.Collection[i]))
                    {
                        data.Events.Collection[i].IsDialogue = false;
                        hadWeridTime = true;
                        weirdTimeEventLines.Add(data.Events.Collection[i].lineNumber - eventLineFirst + 1);
                    }

                    var et = data.Events.Collection[i].Text;
                    if (RemoveEndSpace(et))
                        hadEndSpace = true;

                    if (Check.HadMotionGarbage(et))
                    {
                        et.RemoveAt(0);
                        hadMotionGarbage = true;
                    }

                    RemoveWeridChars(et, ref hadUnusedChar, ref hadWeridSpace, etsb);
                }

                if (hadWeridTime)
                {
                    records.Append($" comment start > end event lines: {string.Join(", ", weirdTimeEventLines)};");
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
                if (hadEndSpace)
                {
                    records.Append(" remove end space chars;");
                }
                RecordRemoveLast(records, 7);
            }

            // process styles if drop unused styles
            if (args.dropUnusedStyles)
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

    public static void ExtractBinaries(AssData data, DirectoryInfo binDir)
    {
        if (!binDir.Exists)
        {
            binDir.Create();
        }

        if (data.Sections.Contains(AssSection.Fonts))
        {
            foreach (var font in data.Fonts)
            {
                var filePath = Path.Combine(binDir.FullName, $"{font.OriginalName}{font.Suffix}");

                if (!new FileInfo(filePath).Exists)
                {
                    font.WriteFile(filePath);
                }
            }
        }

        if (data.Sections.Contains(AssSection.Graphics))
        {
            foreach (var g in data.Graphics)
            {
                var filePath = Path.Combine(binDir.FullName, $"{g.Name}");

                if (!new FileInfo(filePath).Exists)
                {
                    g.WriteFile(filePath);
                }
            }
        }
    }

    private static void RecordRemoveLast(StringBuilder sb, int lineLength)
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

    private static void RemoveWeridChars(List<char[]> et, ref bool hadUnusedChar, ref bool hadWeridSpace, StringBuilder sb)
    {
        var _mod = false;
        for (var j = 0; j < et.Count; j++)
        {
            var blk = et.ToArray()[j];
            foreach (var c in blk)
            {
                if (Check.EventUnusedChars.Contains(c))
                {
                    _mod = true;
                    if (!hadUnusedChar)
                    {
                        hadUnusedChar = true;
                    }
                }
                else if (Check.EventWeirdSpace.Contains(c))
                {
                    _mod = true;
                    sb.Append('\u0020');
                    if (!hadWeridSpace)
                    {
                        hadWeridSpace = true;
                    }
                }
                else
                {
                    sb.Append(c);
                }
            }
            
            if (_mod)
            {
                var _new = new char[sb.Length];
                sb.CopyTo(0, _new, sb.Length);
                et[j] = _new;
            }
            sb.Clear();
        }
    }

    private static bool RemoveEndSpace(List<char[]> et)
    {
        if (et.Count == 0)
            return false;

        var last = et[^1];
        for (var end = last.Length - 1; end >=0; end--)
        {
            if (!char.IsWhiteSpace(last[end]))
            {
                et[^1] = last[..(end + 1)];
                if (end != last.Length - 1)
                    return true;
                else
                    break;
            }
        }
        return false;
    }
}