using Win32.Graphics.DirectWrite;

namespace Mobsub.Font;

public unsafe class FontFaceInfoDWrite : FontFaceInfoBase
{
    public IDWriteFontFaceReference* FontFaceRef;
    public IDWriteFontFace3* FontFace;
}