namespace Mobsub.Helper.Font;

public interface IParseFonts
{
    internal static abstract IEnumerable<FontFaceInfoBase> GetInstalledFontsInfo();
    internal static abstract IEnumerable<FontFaceInfoBase> GetLocalFontsInfo(FileInfo[] localFonts);
    internal static void DumpInfosToCsv(IEnumerable<FontFaceInfoBase> infos, FileInfo csvFile)
    {
        if (csvFile.Exists)
        {
            csvFile.Delete();
        }

        var sw = csvFile.CreateText();
        sw.WriteLine(FontFaceInfoBase.CsvHeader);
        
        foreach (var info in infos)
        {
            if (info.FileInfo is not null)
            {
                // maybe TERMINAL
                if (Platform.IsWindows() && info.FileInfo!.FilePath!.AsSpan().IndexOf(@"PROGRAM FILES\WINDOWSAPPS") > -1)
                {
                    continue;
                }
            }
            sw.WriteLine(info.DumpToCsvLine());
        }
        
        sw.Flush();
        sw.Close();
    }
}