using System;
using Mobsub.Native.FFMS2Binding;
using SkiaSharp;

namespace Mobsub.RainCurtain.Models.Video;

public class Ffms2Video
{
    internal static unsafe IntPtr ConvertToSkImage(Ffms2VideoFrame frame)
    {
        var info = new SKImageInfo(frame.Handle->EncodedWidth, frame.Handle->EncodedHeight, SKColorType.Bgra8888, SKAlphaType.Opaque);
        // var rowBytes = 

        var buffer = frame.Handle->Data;
        byte* pixelPtr = buffer.e0;
        IntPtr pixelData = new IntPtr(pixelPtr);
        // return SKImage.FromPixels(info, pixelData, info.RowBytes);
        return pixelData;
    }
}