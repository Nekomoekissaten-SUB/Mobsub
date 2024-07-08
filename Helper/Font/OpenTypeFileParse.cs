using System.Diagnostics;
using System.Text;
using OTFontFile;
using static OTFontFile.Table_name;

namespace Mobsub.Helper.Font;

public class OpenTypeFileParse(string fontFile)
{
    private string FontFile = fontFile;
    private OTFile FontData = new OTFile();

    public bool Open() => FontData.open(FontFile);
    public bool IsCollection() => FontData.IsCollection();
    public uint GetNumFonts() => FontData.GetNumFonts();
    public OTFont GetFont(uint index) => FontData.GetFont(index)!;
    
    public FontFaceInfoOpenType GetFontInfo(uint index, FontFileInfo fileInfo, LanguageIDWindows curLanguageId)
    {
        var faceInfo = new FontFaceInfoOpenType { FaceIndex = index, FileInfo = fileInfo };
        var font = GetFont(index);

        var nameTable = (Table_name)font.GetTable("name")!;
        var os2Table = (Table_OS2)font.GetTable("OS/2")!;
        var fsSel = os2Table.fsSelection;

        var psNameParams = new GetStringParams
            { EncID = 0xffff, LangID = (ushort)LanguageIDWindows.en_US, NameID = (ushort)NameID.postScriptName };
        var fullNameParams = new GetStringParams
            { EncID = 0xffff, LangID = (ushort)LanguageIDWindows.en_US, NameID = (ushort)NameID.fullName };
        var famNameParams = new GetStringParams
            { EncID = 0xffff, LangID = (ushort)LanguageIDWindows.en_US, NameID = (ushort)NameID.familyName };
        var subFamNameParams = new GetStringParams()
            { EncID = 0xffff, LangID = (ushort)LanguageIDWindows.en_US, NameID = (ushort)NameID.subfamilyName };
        
        var psName = GetNameRecordString(nameTable, psNameParams);
        var fullName = GetNameRecordString(nameTable, fullNameParams);
        var famName = GetNameRecordString(nameTable, famNameParams);
        var subFamName = GetNameRecordString(nameTable, subFamNameParams);

        famNameParams.LangID = (ushort)curLanguageId;
        var famNameL10N = GetNameRecordString(nameTable, famNameParams);

        if (famName == null && famNameL10N == null)
        {
            famNameParams.LangID = null;
            famName = GetNameRecordString(nameTable, famNameParams);
        }
        faceInfo.FamilyNameGdi = famName ?? famNameL10N;
        faceInfo.FamilyNameGdiLocalized = famNameL10N ?? famName;
        if (curLanguageId == LanguageIDWindows.zh_Hans_CN)
        {
            faceInfo.FamilyNameGdiChs = faceInfo.FamilyNameGdiLocalized;
        }
        else
        {
            famNameParams.LangID = (ushort)LanguageIDWindows.zh_Hans_CN;
            faceInfo.FamilyNameGdiChs = GetNameRecordString(nameTable, famNameParams) ?? famName;
        }

        if (subFamName == null)
        {
            subFamNameParams.LangID = null;
            subFamName = GetNameRecordString(nameTable, subFamNameParams);
        }

        if (psName == null)
        {
            psNameParams.LangID = null;
            psName = GetNameRecordString(nameTable, psNameParams);
        }
        faceInfo.PostScriptName = psName;
        
        if (fullName == null)
        {
            fullNameParams.LangID = null;
            fullName = GetNameRecordString(nameTable, fullNameParams);
        }
        faceInfo.FullName = fullName;
        
        // Some mismatches:
        // A1MinchoStd-Bold, fsSelection: 6, usWeightClass=400, dwrite mark 700 (DWRITE_FONT_WEIGHT_BOLD)
        // Candara-Light, fsSelection: 6, usWeightClass=400, dwrite mark 300 (DWRITE_FONT_WEIGHT_LIGHT)
        // FZYOUSK_508R--GBK1-0, fsSelection: 5, usWeightClass=450, dwrite mark 700 (DWRITE_FONT_WEIGHT_BOLD)
        // HiraMinStdN-W5, usWeightClass=500, dwrite mark 700
        faceInfo.Weight = os2Table.usWeightClass >= 1000 ? 999 : os2Table.usWeightClass;
        
        // Some mismatches:
        // LucidaHandwriting-Italic, fsSelection: 6 subfamilyname contains italic, dwrite mark 2
        // DejaVuSans-BoldOblique, fsSelection: 0,5 subfamilyname contains oblique, dwrite mark 1
        // NachlieliCLM-BoldOblique, fsSelection: 0,5 subfamilyname contains oblique, dwrite mark 2
        faceInfo.Style = (fsSel & 0b_1) == 1 ? 2 : (subFamName is not null && subFamName.AsSpan().Contains("Oblique", StringComparison.OrdinalIgnoreCase)) ? 1 : 0;

        // not know how get stretch
        // not know how get familyname (non gdi)

        faceInfo.MaxpNumGlyphs = font.GetMaxpNumGlyphs();
        faceInfo.fsSelection = fsSel;
        
        return faceInfo;
    }

    private struct GetStringParams
    {
        //public ushort PlatID;
        public ushort EncID;
        public ushort? LangID;
        public ushort NameID;
    }
    private struct GetBufResult
    {
        public byte[]? buf;
        public ushort curPlatID;
        public ushort curEncID;
        public ushort curLangID;
    }
    private static string? GetNameRecordString(Table_name nameTable, GetStringParams ids)
    {
        var result = GetBufferMod(nameTable, ids);
        return result.buf == null ? null : DecodeString(result.curPlatID, result.curEncID, result.curLangID, result.buf);
    }
    private static GetBufResult GetBufferMod(Table_name nameTable, GetStringParams ids)
    {
        for (uint i = 0; i < nameTable.NumberNameRecords; i++)
        {
            var nr = nameTable.GetNameRecord(i);
            if (nr == null) { continue; }
            
            if ((nr.PlatformID == (ushort)Table_name.PlatformID.Windows) &&
                ((ids.EncID == 0xffff || nr.EncodingID == ids.EncID) && nr.EncodingID != (ushort)EncodingIDWindows.Unicode_full_repertoire) &&
                ((ids.LangID == 0xffff || nr.LanguageID == ids.LangID) || ids.LangID == null) &&
                nr.NameID == ids.NameID)
            {
                var r = new GetBufResult
                {
                    buf = nameTable.GetEncodedString(nr),
                    curPlatID = nr.PlatformID,
                    curEncID = nr.EncodingID,
                    curLangID = nr.LanguageID,
                };
                return r;
            }
        }

        return new GetBufResult{ buf = null };
    }
    
    
    private static string ConvertUshortToBigEndianBinaryString(ushort value)
    {
        var str = Convert.ToString(value, 2).PadLeft(16, '0');
        var newStr = str.Reverse().ToArray();

        var sb = new StringBuilder();
        for (var i = 0; i < newStr.Count(); i++)
        {
            if (newStr[i] == '1')
            {
                sb.Append(i);
                sb.Append(',');
            }
        }

        return sb.ToString();
    }
    private static void GetAllNameRecords(Table_name nameTable)
    {
        for (uint i = 0; i < nameTable.NumberNameRecords; i++)
        {
            var nr = nameTable.GetNameRecord(i);
            if (nr == null) { continue; }
            Debug.WriteLine($"platform: {nr.PlatformID}, encoding: {nr.EncodingID}, language: {nr.LanguageID}, name: {nr.NameID}");
        }
    }
}
