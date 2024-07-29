using System.Globalization;
using System.Diagnostics;

namespace Mobsub.Helper.Font;

public class FontFaceInfoBase
{
    public uint FaceIndex { get; set; }
    public string? PostScriptName { get; set; }  // NameID 6
    public string? FullName { get; set; }    // NameID 4
    public Dictionary<int, string>? FamilyNames { get; set; }
    public Dictionary<int, string>? FamilyNamesGdi { get; set; }          // NameID 1
    public int Weight { get; set; }
    public int Style { get; set; }
    public int? Stretch { get; set; }
    public FontFileInfo? FileInfo { get; set; }

    internal const string CsvHeader = "PostScriptName,FullName,FamilyNames,FamilyNamesGdi,Weight,Style,Stretch," +
                                      $"FilePath,FileSize,LastWriteTime";
    
    internal string DumpToCsvLine()
    {
        var familyNames = FamilyNames is null ? string.Empty : string.Join("|", FamilyNames.Select(kvp => $"{new CultureInfo(kvp.Key).Name}:{kvp.Value}"));
        var familyNamesGdi = FamilyNamesGdi is null ? string.Empty : string.Join("|", FamilyNamesGdi.Select(kvp => $"{new CultureInfo(kvp.Key).Name}:{kvp.Value}"));

        return $"{PostScriptName},{FullName},{familyNames},{familyNamesGdi},{Weight},{Style},{Stretch}," +
               $"{(FileInfo is null ? "" : FileInfo.FilePath)}," +
               $"{(FileInfo is null ? "" : FileInfo.FileSize)}," +
               $"{(FileInfo is null ? "" : FileInfo.LastWriteTime)}";
    }

    public void DebugWriteToCmd(string? extra)
    {
        var famNames = FamilyNames is null ? string.Empty : string.Join("|", FamilyNames!.Values.Distinct());
        var famNameGdi = FamilyNamesGdi is null ? string.Empty : string.Join("|", FamilyNamesGdi!.Values.Distinct());
        
        Debug.WriteLine($"psname: {PostScriptName}, fullname: {FullName}, weight: {Weight}, stretch: {Stretch}, style: {Style}, " +
                        $"faceIndex: {FaceIndex}, famName: {famNames}, famNameGdi: {famNameGdi}" +
                        $"{extra}");
    }
}

public class FontFileInfo
{
    public string? FilePath { get; set; }
    public ulong? FileSize { get; set; }
    public DateTime? LastWriteTime { get; set; }
}