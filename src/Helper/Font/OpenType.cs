using System.Diagnostics;
using System.Globalization;
using static OTFontFile.Table_name;

namespace Mobsub.Helper.Font;

public class OpenType : IParseFonts
{
    private static readonly LanguageIDWindows curLanguageId = GetCurrentLanguageId();
    
    public static IEnumerable<FontFaceInfoBase> GetInstalledFontsInfo()
    {
        ICollection<FontFaceInfoOpenType> infos = [];

        var dirs = Platform.GetInstalledFontDir();

        foreach (var dir in dirs)
        {
            var dirInfo = new DirectoryInfo(dir);
            if (!dirInfo.Exists){ continue; }

            var fileInfos = dirInfo.GetFiles();
            GetFontsInfo(fileInfos, infos);
        }
        
        return infos;
    }

    public static IEnumerable<FontFaceInfoBase> GetLocalFontsInfo(FileInfo[] localFonts)
    {
        ICollection<FontFaceInfoOpenType> infos = [];
        GetFontsInfo(localFonts, infos);
        return infos;
    }

    private static void GetFontsInfo(FileInfo[] fileInfos, ICollection<FontFaceInfoOpenType> infos)
    {
        foreach (var fileInfo in fileInfos)
        {
            if (!IParseFonts.IsSupportFonts(fileInfo)){ continue; }
                
            var fp = new OpenTypeFileParse(fileInfo.FullName);
            if (!fp.Open()) { continue; }
            // throw new FormatException();

            var fontFileInfo = new FontFileInfo()
            {
                FilePath = fileInfo.FullName,
                FileSize = (ulong)fileInfo.Length,
                LastWriteTime = fileInfo.LastWriteTime,
            };
            for (uint i = 0; i < fp.GetNumFonts(); i++)
            {
                infos.Add(fp.GetFontInfo(i, fontFileInfo, curLanguageId));
            }
        }
    }
    
    private static LanguageIDWindows GetCurrentLanguageId()
    {
        var lcid = (ushort)CultureInfo.CurrentCulture.LCID;
        if (Enum.IsDefined(typeof(LanguageIDWindows), lcid))
        {
            return (LanguageIDWindows)lcid;
        }
        else
        {
            return LanguageIDWindows.en_US;
        }
    }
}
