using System.Text;
using Mobsub.SubtitleParse.AssTypes;
using Mobsub.SubtitleParse;

namespace Mobsub.Ikkoku.SubtileProcess;

internal class Check
{
    internal static void PrintUnnormalAssTags(List<AssEvent> events, bool verbose, string checkType)
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

    internal static bool WeirdTimeOneLine(AssEvent evt) => evt.IsDialogue && (evt.Start.CompareTo(evt.End) > 0);

    /// <summary>
    /// such like {=} {=0} {=99}
    /// </summary>
    /// <param name="et">Event.Text</param>
    /// <returns></returns>
    internal static bool HadMotionGarbage(List<char[]> et) => et.Count > 0 && AssTagParse.IsOverrideBlock(et[0].AsSpan()) && et[0][1] == '=' && ((et[0].Length > 3 && char.IsDigit(et[0][2])) || et[0].Length == 3);

    internal static readonly char[] EventUnusedChars = ['\u200E', '\u200F', '\u200B'];
    internal static readonly char[] EventWeirdSpace = ['\u00A0', '\ufeff'];

    internal static void CheckWeirdChars(List<char[]> et, out bool hadUnusedChar, out bool hadWeridSpace)
    {
        hadUnusedChar = false;
        hadWeridSpace = false;
        for (var i = 0; i < et.Count; i++)
        {
            var blk = et.ToArray()[i];
            foreach (var c in blk)
            {
                if (EventUnusedChars.Contains(c))
                {
                    hadUnusedChar = true;
                }
                else if (EventWeirdSpace.Contains(c))
                {
                    hadWeridSpace = true;
                }
            }
        }
    }

}