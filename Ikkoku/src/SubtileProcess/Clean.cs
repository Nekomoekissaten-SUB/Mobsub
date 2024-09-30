using System.Diagnostics;
using System.Text;
using Mobsub.SubtitleParse.AssTypes;
using Mobsub.SubtitleParse.AssUtils;

namespace Mobsub.Ikkoku.SubtileProcess;

public class Clean
{
    public struct CleanAssArgs
    {
        // Script Info
        internal bool keepComment;
        internal bool renameTitle;
        internal bool addLayoutRes;
        internal bool deleteFanhuaji;

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
        var removeRecords = (from s1 in rmSections
            where data.Sections.Contains(s1)
            select s1 switch
            {
                AssSection.Fonts => "Fonts",
                AssSection.Graphics => "Graphics",
                AssSection.AegisubProjectGarbage => "Aegisub Project Garbage",
                AssSection.AegisubExtradata => "Aegisub Extradata",
                _ => "Unknown Section"
            }).ToList();
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
        // delete Fanhuaji comment line
        if (args.deleteFanhuaji && (data.ScriptInfo.Others.Count > 0))
        {
            foreach (var (k,v) in data.ScriptInfo.Others)
            {
                if (k == "Comment" && v.StartsWith("Processed by 繁化姬"))
                {
                    data.ScriptInfo.Others.Remove(k);
                    data.ScriptInfo.Orders.Remove(k);
                    records.Append(" remove Fanhuaji info;");
                    break;
                }
            }
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
            var addLayoutX = data.ScriptInfo.Orders.Add("LayoutResX");
            var addLayoutY = data.ScriptInfo.Orders.Add("LayoutResY");
            if (addLayoutX || addLayoutY)
            {
                records.Append(" add LayoutResX/Y;");
            }
        }
        RecordRemoveLast(records, 12);
    
        if (args.processEvents || args.dropUnusedStyles)
        {
            var assAnlz = new AssAnalyze(data);
            var usedStyles = assAnlz.GetUsedStyles();
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
                var hadWeirdTime = false;
                var weirdTimeEventLines = new List<int>();
                var eventLineFirst = data.Events.Collection[0].lineNumber;
                var hadMotionGarbage = false;
                var hadUnusedChar = false;
                var hadWeirdSpace = false;
                var hadEndSpace = false;
                
                foreach (var evt in data.Events.Collection)
                {
                    if (Check.WeirdTimeOneLine(evt))
                    {
                        evt.IsDialogue = false;
                        hadWeirdTime = true;
                        weirdTimeEventLines.Add(evt.lineNumber - eventLineFirst + 1);
                    }
                    
                    var text = evt.Text.AsSpan();

                    if (char.IsWhiteSpace(text[^1]))
                    {
                        text = text.TrimEnd();
                        hadEndSpace = true;
                    }
                    
                    // now only remove first motion garbage
                    if (Check.IsMotionGarbage(text[evt.TextRanges[0]]))
                    {
                        text = text.TrimStart(text[evt.TextRanges[0]]);
                        evt.TextRanges = evt.TextRanges[1..];
                        hadMotionGarbage = true;
                    }

                    if (RemoveWeirdChars(text, ref hadUnusedChar, ref hadWeirdSpace, etsb))
                    {
                        evt.Text = etsb.ToString();
                        evt.UpdateTextRanges();
                    }
                    else
                    {
                        evt.Text = text.ToString();
                    }
                }

                if (hadWeirdTime)
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
                if (hadWeirdSpace)
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
    private static bool RemoveWeirdChars(ReadOnlySpan<char> text, ref bool hadUnusedChar, ref bool hadWeridSpace, StringBuilder sb)
    {
        var mod = false;
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (Check.EventUnusedChars.Contains(c))
            {
                mod = true;
                if (!hadUnusedChar)
                {
                    hadUnusedChar = true;
                }
            }
            else if (Check.EventWeirdSpace.Contains(c))
            {
                mod = true;
                sb.Append('\u0020');
                if (!hadWeridSpace)
                {
                    hadWeridSpace = true;
                }
            }
            else if (c == '\uFE0F')
            {
                // Now libass not support color emoji (https://github.com/libass/libass/issues/381), the char is meaningless
                mod = true;
                if (!hadUnusedChar)
                {
                    hadUnusedChar = true;
                }
            }
            else if (char.IsHighSurrogate(c))
            {
                if (i + 1 < text.Length && char.IsLowSurrogate(text[i + 1]))
                {
                    sb.Append(c);
                    sb.Append(text[i + 1]);
                    i++;
                }
                else
                {
                    mod = true;
                    if (!hadUnusedChar)
                    {
                        hadUnusedChar = true;
                    }
                }
            }
            else
            {
                sb.Append(c);
            }
        }

        return mod;
    }
    
}