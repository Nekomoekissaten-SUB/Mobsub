using Mobsub.AssFormat;
using Mobsub.Utils;
using System.Data;
using System.Text.RegularExpressions;

namespace Mobsub;

public class AssParse
{
    public static Dictionary<string, SectionValue> ExtractSection(string[] fileData)
    {
        var sectionInfo = new Dictionary<string, SectionValue>();
        var list = new List<SectionValue>();

        /// maybe section duplicate, need try-catch?
        for (int i = 0; i < fileData.Length; i++)
        {
            if (Regex.IsMatch(fileData[i], @"(^\[.*\]$)")) /// ^\[.*\]$  /// ^\[[0-9a-zA-Z​\s*]+\]$|^\[\]$
            {
                string sectionName = fileData[i][1..^1];
                if (fileData[i + 1].Split(':', 2)[0] == "Format")
                {
                    list.Add(new SectionValue() { IsTableau = true, LineNumber = i });
                }
                else
                {
                    list.Add(new SectionValue() { IsTableau = false, LineNumber = i });
                }

                try
                {
                    sectionInfo.Add(sectionName, list[0]);
                }
                catch (Exception ex)
                {
                    throw new Exception($"Your ASS script has multi sections: ${sectionName}.", ex);
                }

                list.Clear();
            }
        }
        return sectionInfo;
    }
    public static string[] GetUsedStyles(DataTable eventTable)
    {
        /// Skip comment lines
        var styleC = from c in eventTable.AsEnumerable()
                     where c.Field<string>("Format") == "Dialogue"
                     select c.Field<string>("Style");
        var styles = styleC.ToList();
        /// var styles = eventTable.AsEnumerable().Select(c => c.Field<string>("Style")).ToList();

        foreach (var line in eventTable.AsEnumerable().Select(c => c.Field<string>("Text")))
        {
            if (line is not null)
            {
                styles.AddRange(from string style in Regex.Matches(line, @"{[^{}]*\r[^{}]*")
                                select style.ToString());
                //foreach (string style in Regex.Matches(line, @"{[^{}]*\r[^{}]*").Cast<string>())
                //{
                //    styles.Add(style.ToString());
                //}
            }
        }
        return styles.Distinct().ToArray();
    }
    public static string[] GetDefinedStyles(DataTable styleTable)
    {
#pragma warning disable CS8619 // Nullability of reference types in value doesn't match target type.
        return styleTable.AsEnumerable().Select(c => c.Field<string>("Name")).ToArray();
#pragma warning restore CS8619 // Nullability of reference types in value doesn't match target type.
    }
    //public static List<string[]> GetUsedFonts(DataTable eventTable, DataTable styleTable)
    //{

    //}

    public static AssData.GernalSection ParseGernal(string[] lines)
    {
        var section = new AssData.GernalSection();
        foreach (string line in lines)
        {
            if (line.StartsWith(";") || line.StartsWith("Comment"))
            {
                /// Some script info begin is not key value pair
                section.Add(line, "");
            }
            else
            {
                string[] arr = line.Split(':', 2);
                section.Add(arr[0], arr[1].Trim());
            }
        }
        return section;
    }
    public static AssData.GernalSection ParseSpecial(string[] lines)
    {
        /// storing binary data as text, a variant of UUEncoding
        var section = new AssData.GernalSection();
        var keyList = new List<string>();
        var lineList = new List<int>();

        for (int i = 0; i < lines.Length; i++)
        {
            if (Regex.IsMatch(lines[i], @"^(filename|fontname)"))
            {
                keyList.Add(lines[i].Split(':', 2)[1].Trim());
                lineList.Add(i);
            }
        }

        for (int i = 0; i < keyList.Count; i++)
        {
            if (i == keyList.Count - 1)
            {
                section[keyList[i]] = string.Join("", lines[(lineList[i] + 1)..^0]);
            }
            else
            {
                section[keyList[i]] = string.Join("", lines[(lineList[i] + 1)..lineList[i + 1]]);
            }
        }
        return section;
    }
    public static DataTable ParseTable(string[] lines)
    {
        var section = new DataTable { };
        var regex = new Regex(@"(?<![0-9]):|,");
        int max_num = regex.Matches(lines[0]).Count + 1;

        for (int i = 0; i < lines.Length; i++)
        {
            if (i == 0)
            {
                foreach (string name in regex.Split(lines[i], max_num))
                {
                    section.Columns.Add(name.Trim());
                }
            }
            else
            {
                /// Remove spaces at the beginning and end of a line, may like VSFilter
                var sl = new List<string> { };
                foreach (string s in regex.Split(lines[i], max_num))
                {
                    sl.Add(s.Trim());
                }
                section.Rows.Add(sl.ToArray());
            }
        }

        return section;
    }

    public static TimeOnly ParseTime(string st)
    {
        /// hours:minutes:seconds:centiseconds
        var tArr = new Regex(@"(\d+):(\d+):(\d+)\.(\d+)").Split(st);
        return new TimeOnly(int.Parse(tArr[1]), int.Parse(tArr[2]), int.Parse(tArr[3]), int.Parse($"{tArr[4]}0"));
    }
    public static string ToTime(TimeOnly t)
    {
        return t.ToString("H:mm:ss.ff");
    }

    public static Dictionary<string, AssData> Parse(string[] fileData)
    {
        var tableauSection = new List<string>();
        var result = new Dictionary<string, AssData>();
        var allSection = ExtractSection(fileData);

        foreach (KeyValuePair<string, SectionValue> kvp in allSection)
        {
            if (kvp.Value.IsTableau is true)
            {
                tableauSection.Add(kvp.Key);
            }
        }

        for (int i = 0; i < allSection.Count; i++)
        {
            var lines = new List<string>();
            var kvp = allSection.ElementAt(i);

            /// First line is section name
            int bn = kvp.Value.LineNumber + 1;
            if (i == (allSection.Count - 1))
            {
                lines.AddRange(fileData[bn..^0]);
            }
            else
            {
                lines.AddRange(fileData[bn..allSection.ElementAt(i + 1).Value.LineNumber]);
            }

            string[] linesA = lines.Where(x => !string.IsNullOrEmpty(x)).ToArray();

            if (tableauSection.Contains(kvp.Key))
            {
                result.Add(kvp.Key, new AssData() { Table = ParseTable(linesA) });
            }
            else if (AssConst.binSectionArr.Contains(kvp.Key))
            {
                result.Add(kvp.Key, new AssData() { Gernal = ParseSpecial(linesA) });
            }
            else
            {
                result.Add(kvp.Key, new AssData() { Gernal = ParseGernal(linesA) });
            }
        }

        return result;
    }
    public static Dictionary<FileInfo, Dictionary<string, AssData>> ParseMulti(FileSystemInfo path)
    {
        var result = new Dictionary<FileInfo, Dictionary<string, AssData>>();
        switch (path)
        {
            case FileInfo file:
                result[file] = Parse(Files.Read(file));
                return result;
            case DirectoryInfo dir:
                foreach (FileInfo file in Files.Traversal(dir, ".ass"))
                {
                    if (file is not null)
                    {
                        result[file] = Parse(Files.Read(file));
                    }
                }
                return result;
            default:
                throw new IOException();
        }
    }

    public static string[] JoinSections(Dictionary<string, AssData> data)
    {
        var result = new List<string>();

        foreach (string sk in data.Keys)
        {
            result.Add($"[{sk}]");

            if (AssConst.binSectionArr.Contains(sk))
            {
                /// result.Add(Regex.Replace(sectionData.Gernal[k], @"(.{80}{)", $"$1{Environment.NewLine}"));
                /// Wait BinEncode
            }
            else
            {
                if (data[sk].Table is null)
                {
                    result.AddRange(data[sk].Gernal.Keys.Select(k => $"{k}: {data[sk].Gernal[k]}"));
                    result.Add("");
                }
                else
                {
                    int cols = data[sk].Table.Columns.Count;
                    string[] arrTmp = new string[cols];

                    for (int i = 0; i < cols; i++)
                    {
                        arrTmp[i] = data[sk].Table.Columns[i].ColumnName;
                    }

                    string s = new Regex(",").Replace(string.Join(", ", arrTmp), ":", 1);
                    result.Add(s);

                    foreach (DataRow dr in data[sk].Table.Rows)
                    {
                        Array.Clear(arrTmp, 0, cols);
                        for (int i = 0; i < data[sk].Table.Columns.Count; i++)
                        {
                            arrTmp[i] = $"{dr[i]}";
                        }
                        s = new Regex(",").Replace(string.Join(",", arrTmp), ": ", 1);
                        result.Add(s);
                    }
                    result.Add("");
                }
            }
        }
        result.RemoveAt(result.Count - 1);
        return result.ToArray();

    }
}

/// <summary>
/// TODO: color, timestamp, font…tag
/// </summary>
/// https://github.com/Aegisub/Aegisub/blob/6f546951b4f004da16ce19ba638bf3eedefb9f31/libaegisub/ass/uuencode.cpp
/// https://referencesource.microsoft.com/#mscorlib/system/convert.cs,2345
/*public class BinFile
{
    /// Same as UUEncode, UUDecode
    public static Byte[] BinDecode(string text)
    {
        var binFile = new Byte[] {};
        return binFile;
    }
    public static string BinEncode(Byte[] file)
    {
        string text = "";
        return text;
    }
}
*/

public class AssGernate
{
    public static Dictionary<string, AssData> GetTemplate(string title, string assVer)
    {
        var Template = new Dictionary<string, AssData>();
        var infoTemplate = new AssData.GernalSection() { };
        title ??= "Advanced SubStation Alpha Script";
        string styleVer = "";
        var assStyleL = new List<string>();
        var assEventL = new List<string>();

        infoTemplate.Add("Title", title);

        if (assVer == "4+" || assVer is null)
        {
            styleVer = "V4+ Styles";
            infoTemplate.Add("ScriptType", "v4.00 +");
            assStyleL.Add(AssConst.assStyle);
            assEventL.Add(AssConst.assEvent);
        }
        else if (assVer == "5")
        {
            styleVer = "V5 Styles";
            infoTemplate.Add("ScriptType", "v5");
            assStyleL.Add("");
            assEventL.Add("");
        }

        infoTemplate.Add("WrapStyle", "0");

        /*
            ScaledBorderAndShadow: yes
            PlayResX: 1280
            PlayResY: 720
            YCbCr Matrix: TV.709
         */

        Template.Add("Script Info", new AssData() { Gernal = infoTemplate });
        Template.Add(styleVer, new AssData() { Table = AssParse.ParseTable(assStyleL.ToArray()) });
        Template.Add("Events", new AssData() { Table = AssParse.ParseTable(assEventL.ToArray()) });

        return Template;
    }
}