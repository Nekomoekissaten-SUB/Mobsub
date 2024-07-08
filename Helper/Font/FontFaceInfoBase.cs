using System.Text;

namespace Mobsub.Helper.Font;

public class FontFaceInfoBase
{
    public uint FaceIndex { get; set; }
    public string? PostScriptName { get; set; }  // NameID 6
    public string? FullName { get; set; }    // NameID 4
    public string? FamilyName { get; set; }
    public string? FamilyNameLocalized { get; set; }
    public string? FamilyNameGdi { get; set; }          // NameID 1
    public string? FamilyNameGdiLocalized { get; set; } // NameID 1
    public int Weight { get; set; }
    public int Style { get; set; }
    public int? Stretch { get; set; }
    public FontFileInfo? FileInfo { get; set; }

    internal const string CsvHeader = "PostScriptName,FullName,FamilyName,FamilyNameLocalized,FamilyNameGdi,FamilyNameGdiLocalized,Weight,Style,Stretch," +
                                      $"FilePath,FileSize,LastWriteTime";
    
    internal string DumpToCsvLine()
    {
        return $"{PostScriptName},{FullName},{FamilyName},{FamilyNameLocalized},{FamilyNameGdi},{FamilyNameGdiLocalized},{Weight},{Style},{Stretch}," +
               $"{(FileInfo is null ? "" : FileInfo.FilePath)}," +
               $"{(FileInfo is null ? "" : FileInfo.FileSize)}," +
               $"{(FileInfo is null ? "" : FileInfo.LastWriteTime)}";
    }
}

public class FontFileInfo
{
    public string? FilePath { get; set; }
    public ulong? FileSize { get; set; }
    public DateTime? LastWriteTime { get; set; }
}