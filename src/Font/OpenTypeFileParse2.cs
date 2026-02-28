using OTFontFile2;
using OTFontFile2.Tables;

namespace Mobsub.Font;

public sealed class OpenTypeFileParse2(string fontFile) : IDisposable
{
    private const ushort Wildcard = 0xffff;

    // Windows name encoding id 10: Unicode full repertoire (UCS-4). Keep parity with old OTFontFile-based parser.
    private const ushort UnicodeFullRepertoireEncodingIdWindows = 10;

    private readonly string fontFile = fontFile;
    private SfntFile fontData = null!;
    private bool opened;

    public bool Open()
    {
        if (opened) { return true; }

        if (SfntFile.TryOpen(fontFile, out var file, out _))
        {
            fontData = file;
            opened = true;
            return true;
        }

        return false;
    }

    public bool IsCollection() => opened && fontData.IsTtc;
    public uint GetNumFonts() => opened ? (uint)fontData.FontCount : 0;
    public SfntFont GetFont(uint index) => fontData.GetFont((int)index);

    public FontFaceInfoOpenType GetFontInfo(uint index, FontFileInfo fileInfo, ushort curLanguageId)
    {
        var faceInfo = new FontFaceInfoOpenType { FaceIndex = index, FileInfo = fileInfo };
        if (!opened)
        {
            return faceInfo;
        }

        var font = GetFont(index);

        if (!font.TryGetName(out var nameTable) || !font.TryGetOs2(out var os2Table))
        {
            return faceInfo;
        }

        faceInfo.FamilyNamesGdi = GetNameRecordStrings(nameTable, NameTable.NameId.FamilyName);

        // Keep behavior similar to the OTFontFile-based parser:
        // 1) prefer en-US, 2) fallback to any language.
        // curLanguageId is currently unused (same as old implementation).
        const ushort langEnUs = 0x0409;

        var psNameParams = new GetStringParams
            { EncID = Wildcard, LangID = langEnUs, NameID = (ushort)NameTable.NameId.PostScriptName };
        var fullNameParams = new GetStringParams
            { EncID = Wildcard, LangID = langEnUs, NameID = (ushort)NameTable.NameId.FullName };
        var subFamNameParams = new GetStringParams
            { EncID = Wildcard, LangID = langEnUs, NameID = (ushort)NameTable.NameId.SubfamilyName };

        var psName = GetNameRecordString(nameTable, psNameParams);
        var fullName = GetNameRecordString(nameTable, fullNameParams);
        var subFamName = GetNameRecordString(nameTable, subFamNameParams);

        if (subFamName is null)
        {
            subFamNameParams.LangID = null;
            subFamName = GetNameRecordString(nameTable, subFamNameParams);
        }

        if (psName is null)
        {
            psNameParams.LangID = null;
            psName = GetNameRecordString(nameTable, psNameParams);
        }
        faceInfo.PostScriptName = psName;

        if (fullName is null)
        {
            fullNameParams.LangID = null;
            fullName = GetNameRecordString(nameTable, fullNameParams);
        }
        faceInfo.FullName = fullName;

        var fsSel = os2Table.FsSelection;

        // Keep parity with old logic.
        faceInfo.Weight = os2Table.UsWeightClass >= 1000 ? 999 : os2Table.UsWeightClass;
        faceInfo.Style = (fsSel & 0b_1) == 1
            ? 2
            : (subFamName is not null && subFamName.AsSpan().Contains("Oblique", StringComparison.OrdinalIgnoreCase))
                ? 1
                : 0;

        if (font.TryGetMaxp(out var maxpTable))
        {
            faceInfo.MaxpNumGlyphs = maxpTable.NumGlyphs;
        }

        faceInfo.fsSelection = fsSel;

        return faceInfo;
    }

    public void Dispose()
    {
        if (!opened) { return; }
        fontData.Dispose();
        opened = false;
    }

    private struct GetStringParams
    {
        public ushort EncID;
        public ushort? LangID;
        public ushort NameID;
    }

    private static string? GetNameRecordString(NameTable nameTable, GetStringParams ids)
    {
        if (!TryFindNameRecord(nameTable, ids, out var record))
        {
            return null;
        }

        if (!nameTable.TryGetEncodedStringBytes(record, out var buf) || buf.Length == 0)
        {
            return null;
        }

        try
        {
            return nameTable.DecodeString(record);
        }
        catch
        {
            return null;
        }
    }

    private static Dictionary<int, string> GetNameRecordStrings(NameTable nameTable, NameTable.NameId nameId)
    {
        Dictionary<int, string> strs = [];

        for (var i = 0; i < nameTable.Count; i++)
        {
            if (!nameTable.TryGetRecord(i, out var nr)) { continue; }

            if (nr.PlatformId == (ushort)NameTable.PlatformId.Windows &&
                nr.EncodingId != UnicodeFullRepertoireEncodingIdWindows &&
                nr.NameId == (ushort)nameId)
            {
                if (!nameTable.TryGetEncodedStringBytes(nr, out var buf) || buf.Length == 0)
                {
                    strs.TryAdd(nr.LanguageId, string.Empty);
                    continue;
                }

                try
                {
                    var decoded = nameTable.DecodeString(nr);
                    strs.TryAdd(nr.LanguageId, decoded ?? string.Empty);
                }
                catch
                {
                    strs.TryAdd(nr.LanguageId, string.Empty);
                }
            }
        }

        return strs;
    }

    private static bool TryFindNameRecord(NameTable nameTable, GetStringParams ids, out NameTable.NameRecord record)
    {
        for (var i = 0; i < nameTable.Count; i++)
        {
            if (!nameTable.TryGetRecord(i, out record)) { continue; }

            if (record.PlatformId != (ushort)NameTable.PlatformId.Windows)
            {
                continue;
            }

            if ((ids.EncID != Wildcard && record.EncodingId != ids.EncID) ||
                record.EncodingId == UnicodeFullRepertoireEncodingIdWindows)
            {
                continue;
            }

            if (ids.LangID is not null && ids.LangID != Wildcard && record.LanguageId != ids.LangID)
            {
                continue;
            }

            if (record.NameId != ids.NameID)
            {
                continue;
            }

            return true;
        }

        record = default;
        return false;
    }
}
