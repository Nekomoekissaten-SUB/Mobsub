using System.Text;
using Mobsub.AssTypes;
using Mobsub.SubtitleParse;

namespace Mobsub.Ikkoku;

public partial class SubtileProcess
{
    public static void PrintUnnormalAssTags(List<AssEvent> events, bool verbose, string checkType)
    {
        var eventFirstLineNumber = events[0].lineNumber;

        var modRecords = new HashSet<string>();
        var weirdRecords = new HashSet<string>();

        foreach (var eventLine in events)
        {
            if (eventLine.IsDialogue)
            {
                var lineNumber = eventLine.lineNumber;

                AssTagParse.ClassifyTagsFromLine(eventLine.Text, out StringBuilder modTags, out StringBuilder weirdTags, out StringBuilder normalTags);

                if (verbose)
                {

                    switch (checkType)
                    {
                        case "mod":
                            if (modTags.Length > 0)
                            {
                                Console.WriteLine($"Total line {lineNumber} / Event line {lineNumber - eventFirstLineNumber + 1} find mod tag: {modTags}");
                            }
                            break;
                        case "weird":
                            if (weirdTags.Length > 0)
                            {
                                Console.WriteLine($"Total line {lineNumber} / Event line {lineNumber - eventFirstLineNumber + 1} find weird tag: {weirdTags}");
                            }
                            break;
                        case "both":
                            var s = new StringBuilder($"Total line {lineNumber} / Event line {lineNumber - eventFirstLineNumber + 1}");
                            if (modTags.Length > 0)
                            {
                                s.Append(" find mod tag: ");
                                s.Append(modTags);
                            }
                            if (weirdTags.Length > 0)
                            {
                                s.Append(" find weird tag: ");
                                s.Append(weirdTags);
                            }
                            Console.WriteLine(s);
                            break;
                    }
                }
                else
                {
                    switch (checkType)
                    {
                        case "mod":
                            if (modTags.Length > 0)
                            {
                                DistinctRecordTags(modTags, modRecords);
                            }
                            break;
                        case "weird":
                            if (weirdTags.Length > 0)
                            {
                                DistinctRecordTags(weirdTags, weirdRecords);
                            }
                            break;
                        case "both":
                            if (modTags.Length > 0)
                            {
                                DistinctRecordTags(modTags, modRecords);
                            }
                            if (weirdTags.Length > 0)
                            {
                                DistinctRecordTags(weirdTags, weirdRecords);
                            }
                            break;
                    }
                }
            }
        }

        if (verbose)
        {
            Console.WriteLine("EOF");
        }
        else
        {
            switch (checkType)
            {
                case "mod":
                    if (modRecords.Count > 0)
                    {
                        Console.WriteLine($"Find mod tags: {string.Join(", ", modRecords)}");
                    }
                    else
                    {
                        Console.WriteLine("No mod tags");
                    }
                    break;
                case "weird":
                    if (weirdRecords.Count > 0)
                    {
                        Console.WriteLine($"Find weird tags: {string.Join(", ", weirdRecords)}");
                    }
                    else
                    {
                        Console.WriteLine("No weird tags");
                    }
                    break;
                case "both":
                    if (modRecords.Count > 0)
                    {
                        Console.WriteLine($"Find mod tags: {string.Join(", ", modRecords)}");
                    }
                    else
                    {
                        Console.WriteLine("No mod tags");
                    }
                    if (weirdRecords.Count > 0)
                    {
                        Console.WriteLine($"Find weird tags: {string.Join(", ", weirdRecords)}");
                    }
                    else
                    {
                        Console.WriteLine("No weird tags");
                    }
                    break;
            }
        }
    }

    public static HashSet<string> GetUsedStyles(List<AssEvent> events)
    {
        var styles = new HashSet<string>();
        var str = new StringBuilder();
        foreach (var et in events)
        {
            if (et.IsDialogue)
            {
                var text = et.Text.ToArray();

                styles.Add(et.Style);

                char[] block = [];
                for (var i = 0; i < text.Length; i++)
                {
                    block = text[i];
                    if (block[0] == '{' && block[^1] == '}' && block.Length > 2 && i != text.Length - 1)
                    {
                        foreach (var ca in AssTagParse.GetTagsFromOvrBlock(block))
                        {
                            if (ca[0] == 'r' && ca.Length > 1)
                            {
                                styles.Add(ca[1..].ToString()!);
                            }
                        }
                    }
                }
            }
        }
        // Default style always use
        styles.Add("Default");
        return styles;
    }

    private static void DistinctRecordTags(StringBuilder tags, HashSet<string> records)
    {
        var tag = new StringBuilder();
        var record = true;
        for (int i = 0; i < tags.Length; i++)
        {
            if ((tags[i] is '(' or '-') || (char.IsDigit(tags[i]) && i != 0))
            {
                record = false;
            }
            else if (tags[i] is ':' || (i == tags.Length - 1))
            {
                record = true;
                if (tag.Length > 0)
                {
                    records.Add(tag.ToString());
                }
                tag.Clear();
            }
            else
            {
                if (record)
                {
                    tag.Append(tags[i]);
                }
            }
        }
    }


}