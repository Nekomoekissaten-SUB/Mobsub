using System.Text;
using System.Data;
using System.Runtime.InteropServices;

namespace Mobsub.Utils;

public class Files
{
    public static string[] Read(FileInfo filePath)
    {
        var fileName = filePath.FullName.Trim();
        try
        {
            return File.ReadAllLines(fileName);
        }
        catch (IOException ex)
        {
            throw ex;
        }
    }
    public static void Write(FileInfo newFile, string[] fileData, [Optional] Encoding encoding)
    {
        encoding ??= DetectEncoding.EncodingRefOS();
        File.WriteAllLines(newFile.FullName, fileData, encoding);
    }

    internal static bool CheckFile(FileSystemInfo file)
    {
        return file switch
        {
            FileInfo => true,
            DirectoryInfo => false,
            _ => throw new IOException(),
        };
    }
    internal static List<FileInfo> Traversal(DirectoryInfo path, string ext)
    {
        var assList = new List<FileInfo>();
        foreach (FileInfo NextFile in path.GetFiles())
        {
            if (Path.GetExtension(NextFile.Name) == ext)
            {
                assList.Add(NextFile);
            }
        }
        return assList;
    }
    internal static bool CheckDir(DirectoryInfo dir)
    {
        if (!dir.Exists)
        {
            dir.Create();
            return true;
        }
        else
        {
            return false;
        }
    }
}
/// <summary>
/// ref: https://github.com/AutoItConsulting/text-encoding-detect
/// Maybe not useful
/// </summary>
public class DetectEncoding
{
    /// UTF-16BE with Bom
    private readonly byte[] BigEndianUnicodeBom = { 0xFE, 0xFF };

    /// UTF-16LE with Bom
    private readonly byte[] UnicodeBom = { 0xFF, 0xFE };

    /// UTF-8 with Bom
    private readonly byte[] UTF8Bom = { 0xEF, 0xBB, 0xBF };

    public static Encoding EncodingRefOS()
    {
        switch (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            case false:
                return new UTF8Encoding(false);
            default:
                return Encoding.UTF8;
        }
    }

}

public class DtProcess
{
    internal static DataTable DataTableDistinct(DataTable dt, string columnName)
    {
        /// Distinct identical lines
        var newDt = dt.AsDataView().ToTable(true);
        /// Group by columnName with count(columnName)
        var nameCountQuery = from row in newDt.AsEnumerable()
                             group row by row.Field<string>(columnName) into names
                             select new
                             {
                                 name = names.Key,
                                 nameCount = names.Count()
                             };
        var nameDuplicate = new List<string> { };

        foreach (var n in nameCountQuery)
        {
            if (n.nameCount > 1)
            {
                nameDuplicate.Add(n.name);
            }
        }
        if (nameDuplicate.Count > 0)
        {
            throw new Exception("Have duplicate style names. Please check.");
        }
        else
        {
            return newDt;
        }
    }
}

public class TimecodesConvert
{
    internal static TimeSpan ConvertToSpan(string span, [Optional] string fps)
    {
        if (span.Length > 3)
        {
            var spanNum = span.Substring(0, span.Length - 3);
            return span[^3..] switch
            {
                "frm" => TimeSpan.FromMilliseconds(FrameToMillisecond(Convert.ToInt32(spanNum), UnifiedFps(fps))),
                "sec" => TimeSpan.FromSeconds(Convert.ToDouble(spanNum)),
                "mls" => TimeSpan.FromMilliseconds(Convert.ToDouble(spanNum)),
                "min" => TimeSpan.FromMinutes(Convert.ToDouble(spanNum)),
                _ => TimeSpan.FromSeconds(Convert.ToDouble(span)),
            };
        }
        else
        {
            try
            {
                return TimeSpan.FromSeconds(Convert.ToDouble(span));
            }
            catch (FormatException)
            {
                throw new Exception("Please check your timespan string. It should be end with frm, sec, mls or min if it’s not a number.");
            }   
        }
    }

    internal static double FrameToMillisecond(int frame, decimal fps)
    {
        return (double) (frame / fps) * 1000;
    }

    internal static decimal UnifiedFps(string fpsString)
    {
        string[] Arr24 = { "23.976", "23.98" };
        string[] Arr30 = { "29.970", "29.97" };
        string[] Arr60 = { "59.940", "59.94" };

        if (fpsString.Contains('/'))
        {
            var fpsC = from s in fpsString.Split('/')
                       select Convert.ToDecimal(s.Trim());
            var fpsA = fpsC.ToArray();
            return fpsA[0] / fpsA[1];
        }
        else if (Arr24.Contains(fpsString))
        {
            return 24000 / 1001M;
        }
        else if (Arr30.Contains(fpsString))
        {
            return 30000 / 1001M;
        }
        else if (Arr60.Contains(fpsString))
        {
            return 60000 / 1001M;
        }
        else
        {
            return Convert.ToDecimal(fpsString);
        }
    }
}

public class ConfigYaml
{
    public static readonly string[] Extension = { ".yml", ".yaml" };
}
