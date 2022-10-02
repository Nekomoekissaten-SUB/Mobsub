using System.Collections.Generic;
using System.Data;
using System.Text.RegularExpressions;
using Mobsub.AssFormat;
using Mobsub.Utils;

namespace Mobsub.SubtitleProcess;

public class AssProcess
{
    internal static void Clean(FileInfo file, bool keepCmt, bool dropUnusedStyles, string[]? dropSelectionStyles)
    {
        string[] rmSectionArr = { "Fonts", "Graphics", "Aegisub Project Garbage", "Aegisub Extradata", "" };
        string stylesVer = AssConst.assStyleVer;

        string[] fileData = Files.Read(file);
        var assData = AssParse.Parse(fileData);
        var assDataNew = new Dictionary<string, AssData>(assData);
        var eventDT = assData["Events"].Table;
        var styleDT = assData[stylesVer].Table;
        var newStyleDT = assDataNew[stylesVer].Table;

        var cleanPart = new List<string> { };
        string assName = Path.GetFileNameWithoutExtension(file.Name);

        foreach (string s in rmSectionArr)
        {
            if (assDataNew.ContainsKey(s))
            {
                assDataNew.Remove(s);
                cleanPart.Add(s);
            }
        }

        foreach (string k in assData["Script Info"].Gernal.Keys)
        {
            if (k.StartsWith(";") || k.StartsWith("By") || (!keepCmt & k.StartsWith("Comment")) || assDataNew["Script Info"].Gernal[k] is "")
            {
                assDataNew["Script Info"].Gernal.Remove(k);
                cleanPart.Add("Unused info");
            }
            else if (k == "Title")
            {
                if (assDataNew["Script Info"].Gernal[k] != assName)
                {
                    assDataNew["Script Info"].Gernal[k] = assName;
                    cleanPart.Add("Title");
                }
            }
        }

        var checkStyleResult = CheckStyles(styleDT, eventDT);
        int checkStyleResultKey = checkStyleResult.Keys.ToArray()[0];
        
        if (checkStyleResultKey == 1 )
        {
            var undStyles = new List<string>{ };

            foreach (string u in checkStyleResult[checkStyleResultKey])
            {
                undStyles.Add(u);
            }

            Console.WriteLine($"“{file}” have undefined styles: {string.Join(", ", undStyles)}. Please Check.");
        }
        else
        {
            string optDropStyles = "";
            string[] checkStyleResultVal = checkStyleResult[checkStyleResultKey];
            
            if (dropUnusedStyles)
            {
                if (checkStyleResultVal.Length > 0)
                {
                    newStyleDT = DropStyles(styleDT, checkStyleResultVal);
                    cleanPart.Add("Unused styles");
                    optDropStyles += $"{Environment.NewLine}Drop unused styles: {string.Join(", ", checkStyleResultVal)}";
                }
            }

            if (dropSelectionStyles is not null)
            {
                /// Only drop unused styles
                var dropSelectionStylesChecked = dropSelectionStyles.Intersect(checkStyleResultVal).ToList();

                /// Only way to drop Default style
                if (dropSelectionStyles.Contains("Default"))
                {
                    dropSelectionStylesChecked.Add("Default");
                }

                newStyleDT = DropStyles(newStyleDT, dropSelectionStylesChecked.ToArray());

                if (dropSelectionStylesChecked.Count > 0 )
                {
                    cleanPart.Add("Selection styles");
                    optDropStyles += $"{Environment.NewLine}Drop selection styles: {string.Join(", ", dropSelectionStylesChecked)}";
                }
            }


            char[] unusedChar = new char[] { '\u200E', '\u200F' };
            for (int i = 0; i < eventDT.Rows.Count; i++)
            {
                var orgText = eventDT.Rows[i][eventDT.Columns.Count - 1].ToString();
                var pattern = @"^\{=\d+\}*";
                
                var unusedUnicodeChar = "Unused unicode char";
                var text = string.Concat(orgText.Where(ch => !unusedChar.Contains(ch)).ToArray());
                if (!cleanPart.Contains(unusedUnicodeChar) && !text.Equals(orgText))
                {
                    cleanPart.Add(unusedUnicodeChar);
                }

                var MochaGarbage = "Unused unicode char";
                if (Regex.IsMatch(text, pattern))
                {
                    text = Regex.Replace(text, pattern, "");
                    if (!cleanPart.Contains(MochaGarbage))
                    {
                        cleanPart.Add(MochaGarbage);
                    }
                }

                assDataNew["Events"].Table.Rows[i][eventDT.Columns.Count - 1] = text;
            }

            string[] assNew = AssParse.JoinSections(assDataNew);

            if (cleanPart.Any())
            {
                /// Windows use UTF-8 with Bom, other system use UTF-8
                Files.Write(file, assNew);
                Console.WriteLine($"{file}{Environment.NewLine}" +
                    $"Effect: {string.Join(", ", cleanPart.Distinct().ToArray())}{optDropStyles}");
            }
            else
            {
                Console.WriteLine($"“{file}” no change.");
            }

        }

    }
    internal static Dictionary<string, AssData>[] Merge(Dictionary<string, AssData>[] toAss, Dictionary<string, AssData>[] fromAss, string section)
    {
        /// no resample and convert
        string stylesVer = AssConst.assStyleVer;
        var result = new Dictionary<string, AssData>[toAss.Length];

        if (fromAss.Length > 1 & toAss.Length > 1)
        {
            throw new Exception("Merge: Don’t support many-to-many.");
        }
        else if (fromAss.Length > 1 & toAss.Length == 1)
        {
            var newAss = new Dictionary<string, AssData>(toAss[0]);

            var tSDt = toAss[0][stylesVer].Table.Clone();
            var newEDt = toAss[0]["Events"].Table.Clone();

            foreach (var f in fromAss)
            {
                if (section == "styles" || section == "all")
                {
                    tSDt.Merge(f[stylesVer].Table);
                }
                else if (section == "events" || section == "all")
                {
                    newEDt.Merge(f["Events"].Table);
                }
            }
            /// Only distinct styles section
            var newSDt = DtProcess.DataTableDistinct(tSDt, "Name");

            newAss[stylesVer].Table = newSDt;
            newAss["Events"].Table = newEDt;
            result[0] = newAss;
            return result;
        }
        else if (fromAss.Length == 1 & toAss.Length >= 1)
        {
            for (int i = 0; i < toAss.Length; i++)
            {
                var newAss = new Dictionary<string, AssData>(toAss[i]);
                var newSDt = newAss[stylesVer].Table;
                
                if (section == "styles" || section == "all")
                {
                    newSDt.Merge(fromAss[0][stylesVer].Table);
                }
                
                if (section == "events" || section == "all")
                {
                    newAss["Events"].Table.Merge(fromAss[0]["Events"].Table);
                }
                
                var newSDtDistinct = DtProcess.DataTableDistinct(newSDt, "Name");
                newAss[stylesVer].Table = newSDtDistinct;
                result[i] = newAss;
            }
            return result;
        }
        else
        {
            throw new Exception("Merge: Please check input.");
        }
    }
    internal static DataTable Shift(DataTable et, TimeSpan span)
    {
        foreach (DataRow dr in et.Rows)
        {
            var start = AssParse.ParseTime(Convert.ToString(dr["Start"]));
            var end = AssParse.ParseTime(Convert.ToString(dr["End"]));

            dr["Start"] = AssParse.ToTime(TimecodesConvert.LimitShiftTime(start, span));
            dr["End"] = AssParse.ToTime(TimecodesConvert.LimitShiftTime(end, span));
        }
        return et;
    }

    internal static Dictionary<int, string[]> CheckStyles(DataTable styleTable, DataTable eventTable)
    {
        var result = new Dictionary<int, string[]> { };
        var usedStyles = AssParse.GetUsedStyles(eventTable);
        var definedStyles = AssParse.GetDefinedStyles(styleTable);

        var undefinedS = usedStyles.Except(definedStyles).ToArray();

        if (undefinedS.Length > 0)
        {
            result.Add(1, undefinedS);
        }
        else
        {
            var unusedS = definedStyles.Except(usedStyles).ToArray();
            result.Add(0, unusedS);
        }

        return result;
    }

    internal static DataTable DropStyles(DataTable styleDT, string[]? styles)
    {
        if (styles is null)
        {
            return styleDT;
        }
        else
        {
            for (int i = styleDT.Rows.Count - 1; i >= 0; i--)
            {
                var styleName = (string)styleDT.Rows[i]["Name"];
                if (styles.Contains(styleName))
                {
                    styleDT.Rows.RemoveAt(i);
                }
            }
            return styleDT;
        }
    }
    /// internal static 
}