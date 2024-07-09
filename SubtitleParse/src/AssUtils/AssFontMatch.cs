using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Mobsub.Helper.Font;
using Mobsub.SubtitleParse.AssTypes;

namespace Mobsub.SubtitleParse.AssUtils;

public class AssFontMatch(IEnumerable<FontFaceInfoBase> fontFaceInfos)
{
    private readonly IEnumerable<FontFaceInfoBase> faceInfos = fontFaceInfos;

    public FontFaceInfoBase? GetFontFace(AssFontInfo assFontInfo)
    {
        var faceInfosArr = faceInfos.ToArray();
        var sameFamilyNameFaces = faceInfosArr.Where(face => face.FamilyNameGdi == assFontInfo.Name || face.FamilyNameGdiLocalized == assFontInfo.Name);

        if (!sameFamilyNameFaces.Any()) { return null; }




        return null;
    }
}
