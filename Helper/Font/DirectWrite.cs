using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using CommunityToolkit.Diagnostics;
using Win32;
using Win32.Graphics.DirectWrite;
using DWApis = Win32.Graphics.DirectWrite.Apis;

namespace Mobsub.Helper.Font;

public unsafe class DirectWrite : IParseFonts
{
    static readonly nint localNameEng = Marshal.StringToHGlobalUni("en-us");
    static readonly nint localNameCur = Marshal.StringToHGlobalUni(CultureInfo.CurrentCulture.Name);
    static readonly nint localNameChs = Marshal.StringToHGlobalUni("zh-CN");

    public static IEnumerable<FontFaceInfoBase> GetInstalledFontsInfo()
    {
        // I don’t know where cloud / downloadable fonts come from, maybe cache data
        // Locality is not reliable
        var fontSet = GetSystemFontSet(includeDownloadableFonts:false);
        return GetFontsInfo(fontSet);
    }
    public static IEnumerable<FontFaceInfoBase> GetLocalFontsInfo(FileInfo[] localFonts)
    {
        var fontSet = GetLocalFontSet(localFonts);
        return GetFontsInfo(fontSet);
    }
    public static void DumpInfosToCsv(IEnumerable<FontFaceInfoBase> infos, FileInfo csvFile)
    {
        IParseFonts.DumpInfosToCsv(infos, csvFile);
    }
    

    private static IEnumerable<FontFaceInfoDWrite> GetFontsInfo(IDWriteFontSet* fontSet)
    {
        ICollection<FontFaceInfoDWrite> infos = [];
        var count = fontSet->GetFontCount();

        IDWriteFontFaceReference* fontFaceRef = default;
        IDWriteLocalizedStrings* pStr;
        IDWriteFontFace3* face = null;
        for (uint i = 0; i < count; i++)
        {
            var exists = false;
            var existsPtr = (Bool32*)&exists;

            Marshal.ThrowExceptionForHR(fontSet->GetPropertyValues(i, FontPropertyId.PostscriptName, existsPtr, &pStr));
            var psname = GetLocalizedString(pStr, localNameEng);

            Marshal.ThrowExceptionForHR(fontSet->GetPropertyValues(i, FontPropertyId.FullName, existsPtr, &pStr));
            var fullname = GetLocalizedString(pStr, localNameEng);

            Marshal.ThrowExceptionForHR(fontSet->GetPropertyValues(i, FontPropertyId.Weight, existsPtr, &pStr));
            var weight = GetLocalizedString(pStr, localNameEng);
            
            Marshal.ThrowExceptionForHR(fontSet->GetPropertyValues(i, FontPropertyId.Stretch, existsPtr, &pStr));
            var stretch = GetLocalizedString(pStr, localNameEng);
            
            Marshal.ThrowExceptionForHR(fontSet->GetPropertyValues(i, FontPropertyId.Style, existsPtr, &pStr));
            var style = GetLocalizedString(pStr, localNameEng);

            Marshal.ThrowExceptionForHR(fontSet->GetPropertyValues(i, FontPropertyId.FamilyName, existsPtr, &pStr));
            var famName = GetLocalizedString(pStr, localNameEng);
            var famNameL10N = CultureInfo.CurrentCulture.Name == "en-US" ? famName : GetLocalizedString(pStr, localNameCur);

            Marshal.ThrowExceptionForHR(fontSet->GetPropertyValues(i, FontPropertyId.Win32FamilyName, existsPtr, &pStr));
            var famNameWws = GetLocalizedString(pStr, localNameEng);
            var famNameWwsL10N = CultureInfo.CurrentCulture.Name == "en-US" ? famNameWws : GetLocalizedString(pStr, localNameCur);

            Marshal.ThrowExceptionForHR(fontSet->GetFontFaceReference(i, &fontFaceRef));
            var path = GetFilePath(fontFaceRef);
            var fileSize = fontFaceRef->GetLocalFileSize();
            var faceIndex = fontFaceRef->GetFontFaceIndex();

            Marshal.ThrowExceptionForHR(fontFaceRef->CreateFontFace(&face));
            var glyphCount = face->GetGlyphCount();

            ulong lastWriteTime = default;
            Marshal.ThrowExceptionForHR(fontFaceRef->GetFileTime(&lastWriteTime));
            var dateTime = DateTime.FromFileTime((long)lastWriteTime);

            Debug.WriteLine($"psname: {psname}, fullname: {fullname}, weight: {weight}, stretch: {stretch}, style: {style}, " +
                            $"faceIndex: {faceIndex}, famName: {famName}, famNameL10N: {famNameL10N}, famNameWws: {famNameWws}, famNameWwsL10N: {famNameWwsL10N}, " +
                            $"glyphCount: {glyphCount}");
            
            infos.Add(new FontFaceInfoDWrite
            {
                FaceIndex = faceIndex,
                PostScriptName = psname,
                FullName = fullname,
                FamilyName = famName,
                FamilyNameLocalized = famNameL10N,
                FamilyNameGdi = famNameWws,
                FamilyNameGdiLocalized = famNameWwsL10N,
                Weight = Convert.ToInt32(weight),
                Stretch = Convert.ToInt32(stretch),
                Style = Convert.ToInt32(style),
                FileInfo = new FontFileInfo
                {
                    FilePath = path,
                    FileSize = fileSize,
                    LastWriteTime = dateTime,
                },
                FontFaceRef = fontFaceRef,
                FontFace = face,
            });
        }

        return infos;
    }
    private static IDWriteFontSet* GetSystemFontSet(bool includeDownloadableFonts = false, FactoryType dwFactoryType = FactoryType.Isolated)
    {
        // fontSetMaxVersion = 0;
        var winVersion = Environment.OSVersion.Version;
        if (winVersion.Build >= 20348)
        {
            IDWriteFactory6* pFactory6 = default;
            IDWriteFontSet1* pFontSet1 = default;
            
            fixed (Guid* g = &IDWriteFactory6.IID_IDWriteFactory6)
            {
                Marshal.ThrowExceptionForHR(DWApis.DWriteCreateFactory(dwFactoryType, g, (void**)&pFactory6));
            }
            Marshal.ThrowExceptionForHR(pFactory6->GetSystemFontSet(includeDownloadableFonts, &pFontSet1));
            // fontSetMaxVersion = (byte)((winVersion.Build >= 22621) ? 4 : 3);
            return (IDWriteFontSet*)pFontSet1;
        }
        
        // Minimum supported Windows 10
        IDWriteFactory3* pFactory3 = default;
        IDWriteFontSet* pFontSet = default;
        IDWriteFontCollection1* fc1 = default;
        fixed (Guid* g = &IDWriteFactory3.IID_IDWriteFactory3)
        {
            Marshal.ThrowExceptionForHR(DWApis.DWriteCreateFactory(dwFactoryType, g, (void**)&pFactory3));
        }

        if (!includeDownloadableFonts)
        {
            Marshal.ThrowExceptionForHR(pFactory3->GetSystemFontCollection(includeDownloadableFonts, &fc1, true));
            Marshal.ThrowExceptionForHR(fc1->GetFontSet(&pFontSet));
        }
        else
        {
            Marshal.ThrowExceptionForHR(pFactory3->GetSystemFontSet(&pFontSet));
        }

        return pFontSet;
    }
    private static IDWriteFontSet* GetLocalFontSet(FileInfo[] localFonts)
    {
        // Minimum supported Windows
        IDWriteFactory3* pFactory3;
        IDWriteFactory5* pFactory5;
        IDWriteFontSet* pFontSet = default;
        
        fixed (Guid* g3 = &IDWriteFactory3.IID_IDWriteFactory3)
        fixed (Guid* g5 = &IDWriteFactory5.IID_IDWriteFactory5)
        {
            Marshal.ThrowExceptionForHR(DWApis.DWriteCreateFactory(FactoryType.Isolated, g3, (void**)&pFactory3));
            Marshal.ThrowExceptionForHR(pFactory3->QueryInterface(g5, (void**)&pFactory5));
        }

        if (pFactory5 != null)
        {
            IDWriteFontSetBuilder1* pFontSetBuilder;
            Marshal.ThrowExceptionForHR(pFactory5->CreateFontSetBuilder(&pFontSetBuilder));
            
            foreach (var fontFile in localFonts)
            {
                if (!fontFile.Exists) { continue; }
                var pStr = Marshal.StringToHGlobalUni(fontFile.FullName);

                IDWriteFontFile* pFontFile;
                Marshal.ThrowExceptionForHR(pFactory5->CreateFontFileReference((char*)pStr, null, &pFontFile));
                Marshal.ThrowExceptionForHR(pFontSetBuilder->AddFontFile(pFontFile));
            }
            
            Marshal.ThrowExceptionForHR(pFontSetBuilder->CreateFontSet(&pFontSet));
        }
        else
        {
            IDWriteFontSetBuilder* pFontSetBuilder;
            Marshal.ThrowExceptionForHR(pFactory3->CreateFontSetBuilder(&pFontSetBuilder));

            Bool32 isSupported;
            FontFileType fileType;
            uint numberOfFonts;
            foreach (var fontFile in localFonts)
            {
                if (!fontFile.Exists) { continue; }
                var pStr = Marshal.StringToHGlobalUni(fontFile.FullName);
                
                IDWriteFontFile* pFontFile;
                Marshal.ThrowExceptionForHR(pFactory3->CreateFontFileReference((char*)pStr, null, &pFontFile));
                Marshal.ThrowExceptionForHR(pFontFile->Analyze(&isSupported, &fileType, null, &numberOfFonts));
                
                if (!isSupported) { continue; }

                for (uint i = 0; i < numberOfFonts; i++)
                {
                    IDWriteFontFaceReference* pFontFaceReference;
                    Marshal.ThrowExceptionForHR(pFactory3->CreateFontFaceReference(pFontFile, i, FontSimulations.None, &pFontFaceReference));
                    Marshal.ThrowExceptionForHR(pFontSetBuilder->AddFontFaceReference(pFontFaceReference));
                }
                
                Marshal.ThrowExceptionForHR(pFontSetBuilder->CreateFontSet(&pFontSet));
            }
        }

        return pFontSet;
    }


    private static string? GetFilePath(IDWriteFontFaceReference* fontFaceRef)
    {
        GetLocalFileLoaderAndKey(fontFaceRef, out var fontFileReferenceKey, out var fontFileReferenceKeySize, out var localLoader);
        return GetFilePath(fontFileReferenceKey, fontFileReferenceKeySize, localLoader);
    }
    private static string? GetFilePath(void* fontFileReferenceKey, uint fontFileReferenceKeySize, IDWriteLocalFontFileLoader* localFontFileLoader)
    {
        uint filePathLength = 0;
        Marshal.ThrowExceptionForHR(localFontFileLoader->GetFilePathLengthFromKey(fontFileReferenceKey, fontFileReferenceKeySize, &filePathLength));

        var buffer = Marshal.AllocHGlobal(2 * ((int)filePathLength + 1));
        Marshal.ThrowExceptionForHR(localFontFileLoader->GetFilePathFromKey(fontFileReferenceKey, fontFileReferenceKeySize, (char*)buffer, filePathLength + 1));
        var path = Marshal.PtrToStringUni(buffer);
        Marshal.FreeHGlobal(buffer);
        return path;
    }
    private static string? GetLocalizedString(IDWriteLocalizedStrings* ss, IntPtr localName)
    {
        if (ss is null || ss->GetCount() == 0)
        {
            return null;
        }

        uint index = 0;
        var exists = false;
        Marshal.ThrowExceptionForHR(ss->FindLocaleName((char*)localName, &index, (Win32.Bool32*)&exists));

        if (!exists)
        {
            index = 0;
        }

        uint strLength = 0;
        Marshal.ThrowExceptionForHR(ss->GetStringLength(index, &strLength));
        if (strLength == 0 || strLength == uint.MaxValue)
        {
            return null;
        }

        var buffer = Marshal.AllocHGlobal(2 * ((int)strLength + 1));
        Marshal.ThrowExceptionForHR(ss->GetString(index, (char*)buffer, strLength + 1));
        var str = Marshal.PtrToStringUni(buffer);
        Marshal.FreeHGlobal(buffer);
        return str;
    }
    private static int GetLocalFileLoaderAndKey(IDWriteFontFaceReference* fontFaceRef, out void* fontFileReferenceKey, out uint fontFileReferenceKeySize, out IDWriteLocalFontFileLoader* localFontFileLoader)
    {
        fontFileReferenceKey = null;
        fontFileReferenceKeySize = 0;

        IDWriteFontFile* fontFile = default;
        Marshal.ThrowExceptionForHR(fontFaceRef->GetFontFile(&fontFile));

        if (fontFile == null)
        {
            ThrowHelper.ThrowArgumentNullException();
        }

        return GetLocalFileLoaderAndKey(fontFile, out fontFileReferenceKey, out fontFileReferenceKeySize, out localFontFileLoader);
    }
    private static int GetLocalFileLoaderAndKey(IDWriteFontFile* fontFile, out void* fontFileReferenceKey, out uint fontFileReferenceKeySize, out IDWriteLocalFontFileLoader* localFontFileLoader)
    {
        fontFileReferenceKey = null;
        fontFileReferenceKeySize = 0;

        IDWriteFontFileLoader* loader = null;
        Marshal.ThrowExceptionForHR(fontFile->GetLoader(&loader));

        fixed (Guid* g = &IDWriteLocalFontFileLoader.IID_IDWriteLocalFontFileLoader)
        fixed (IDWriteLocalFontFileLoader** ptr = &localFontFileLoader)
        fixed (void** ptr2 = &fontFileReferenceKey)
        fixed (uint* ptr3 = &fontFileReferenceKeySize)
        {
            Marshal.ThrowExceptionForHR(loader->QueryInterface(g, (void**)ptr));
            Marshal.ThrowExceptionForHR(fontFile->GetReferenceKey(ptr2, ptr3));
        }

        return 0;
    }
}
