using System.Runtime.InteropServices;
using Mobsub.Native.FFMS2Binding.Native;
using static Mobsub.Native.FFMS2Binding.Native.Methods;

namespace Mobsub.Native.FFMS2Binding;

public unsafe class FFmpegSource2 : IDisposable
{
    private bool _disposed = false;

    public FFmpegSource2()
    {
        FFMS_Init(0, 0);
    }
    
    public void Dispose()
    {
        FFMS_Deinit();
        _disposed = true;
    }

    public Ffms2VideoSource ReadVideo(string path)
    {
        var sourceFile = ConvertNative.StringToPtr(path);
        FFMS_ErrorInfo errorInfo;
        var indexer = FFMS_CreateIndexer(sourceFile, &errorInfo);
        var index = FFMS_DoIndexing2(indexer, (int)FFMS_IndexErrorHandling.FFMS_IEH_ABORT, &errorInfo);
        var trackno = FFMS_GetFirstTrackOfType(index, (int)FFMS_TrackType.FFMS_TYPE_VIDEO, &errorInfo);
        var videoSource = FFMS_CreateVideoSource(sourceFile, trackno, index, 0, (int)FFMS_SeekMode.FFMS_SEEK_NORMAL, &errorInfo);
        FFMS_DestroyIndex(index);
        
        // var videoProps = FFMS_GetVideoProperties(videoSource);
        // var numFrames = videoProps->NumFrames;
        var propFrame = FFMS_GetFrame(videoSource, 0, &errorInfo);

        int[] pixfmts = [FFMS_GetPixFmt(ConvertNative.StringToPtr("bgra")), -1];
        fixed (int* ptr = pixfmts)
        {
            var result = FFMS_SetOutputFormatV2(videoSource, ptr, propFrame->EncodedWidth, propFrame->EncodedHeight, (int)FFMS_Resizers.FFMS_RESIZER_BICUBIC, &errorInfo);
            if (result != 0)
            {
                
            }
        }
        
        return new Ffms2VideoSource(videoSource);
    }
}

public unsafe class Ffms2VideoSource(FFMS_VideoSource* source) : IDisposable
{
    private bool _disposed = false;
    public FFMS_VideoProperties Props = Marshal.PtrToStructure<FFMS_VideoProperties>((IntPtr)FFMS_GetVideoProperties(source));
    
    public void Dispose()
    {
        FFMS_DestroyVideoSource(source);
        _disposed = true;
    }

    public Ffms2VideoFrame GetFrame(int frameNumber)
    {
        FFMS_ErrorInfo errorInfo;
        var frame = FFMS_GetFrame(source, frameNumber, &errorInfo);
        return new Ffms2VideoFrame(frame, frameNumber);
    }
}

public unsafe class Ffms2VideoFrame(FFMS_Frame* frame, int frameNumber)
{
    public FFMS_Frame* Handle = frame;
}