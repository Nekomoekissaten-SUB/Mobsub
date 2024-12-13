using System.Runtime.InteropServices;

namespace Mobsub.Native.FFMS2Binding.Native;

// ReSharper disable InconsistentNaming
public static unsafe partial class Methods
{
    [DllImport(Library.FFmpegSource2Dll, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern void FFMS_Init(int param0, int param1);

    [DllImport(Library.FFmpegSource2Dll, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern void FFMS_Deinit();

    [DllImport(Library.FFmpegSource2Dll, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int FFMS_GetVersion();

    [DllImport(Library.FFmpegSource2Dll, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int FFMS_GetLogLevel();

    [DllImport(Library.FFmpegSource2Dll, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern void FFMS_SetLogLevel(int Level);

    [DllImport(Library.FFmpegSource2Dll, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern FFMS_VideoSource* FFMS_CreateVideoSource([NativeTypeName("const char *")] sbyte* SourceFile, int Track, FFMS_Index* Index, int Threads, int SeekMode, FFMS_ErrorInfo* ErrorInfo);

    [DllImport(Library.FFmpegSource2Dll, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern FFMS_AudioSource* FFMS_CreateAudioSource([NativeTypeName("const char *")] sbyte* SourceFile, int Track, FFMS_Index* Index, int DelayMode, FFMS_ErrorInfo* ErrorInfo);

    [DllImport(Library.FFmpegSource2Dll, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern FFMS_AudioSource* FFMS_CreateAudioSource2([NativeTypeName("const char *")] sbyte* SourceFile, int Track, FFMS_Index* Index, int DelayMode, int FillGaps, double DrcScale, FFMS_ErrorInfo* ErrorInfo);

    [DllImport(Library.FFmpegSource2Dll, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern void FFMS_DestroyVideoSource(FFMS_VideoSource* V);

    [DllImport(Library.FFmpegSource2Dll, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern void FFMS_DestroyAudioSource(FFMS_AudioSource* A);

    [DllImport(Library.FFmpegSource2Dll, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("const FFMS_VideoProperties *")]
    public static extern FFMS_VideoProperties* FFMS_GetVideoProperties(FFMS_VideoSource* V);

    [DllImport(Library.FFmpegSource2Dll, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("const FFMS_AudioProperties *")]
    public static extern FFMS_AudioProperties* FFMS_GetAudioProperties(FFMS_AudioSource* A);

    [DllImport(Library.FFmpegSource2Dll, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("const FFMS_Frame *")]
    public static extern FFMS_Frame* FFMS_GetFrame(FFMS_VideoSource* V, int n, FFMS_ErrorInfo* ErrorInfo);

    [DllImport(Library.FFmpegSource2Dll, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("const FFMS_Frame *")]
    public static extern FFMS_Frame* FFMS_GetFrameByTime(FFMS_VideoSource* V, double Time, FFMS_ErrorInfo* ErrorInfo);

    [DllImport(Library.FFmpegSource2Dll, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int FFMS_GetAudio(FFMS_AudioSource* A, void* Buf, [NativeTypeName("int64_t")] long Start, [NativeTypeName("int64_t")] long Count, FFMS_ErrorInfo* ErrorInfo);

    [DllImport(Library.FFmpegSource2Dll, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int FFMS_SetOutputFormatV2(FFMS_VideoSource* V, [NativeTypeName("const int *")] int* TargetFormats, int Width, int Height, int Resizer, FFMS_ErrorInfo* ErrorInfo);

    [DllImport(Library.FFmpegSource2Dll, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern void FFMS_ResetOutputFormatV(FFMS_VideoSource* V);

    [DllImport(Library.FFmpegSource2Dll, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int FFMS_SetInputFormatV(FFMS_VideoSource* V, int ColorSpace, int ColorRange, int Format, FFMS_ErrorInfo* ErrorInfo);

    [DllImport(Library.FFmpegSource2Dll, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern void FFMS_ResetInputFormatV(FFMS_VideoSource* V);

    [DllImport(Library.FFmpegSource2Dll, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern FFMS_ResampleOptions* FFMS_CreateResampleOptions(FFMS_AudioSource* A);

    [DllImport(Library.FFmpegSource2Dll, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int FFMS_SetOutputFormatA(FFMS_AudioSource* A, [NativeTypeName("const FFMS_ResampleOptions *")] FFMS_ResampleOptions* options, FFMS_ErrorInfo* ErrorInfo);

    [DllImport(Library.FFmpegSource2Dll, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern void FFMS_DestroyResampleOptions(FFMS_ResampleOptions* options);

    [DllImport(Library.FFmpegSource2Dll, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern void FFMS_DestroyIndex(FFMS_Index* Index);

    [DllImport(Library.FFmpegSource2Dll, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int FFMS_GetFirstTrackOfType(FFMS_Index* Index, int TrackType, FFMS_ErrorInfo* ErrorInfo);

    [DllImport(Library.FFmpegSource2Dll, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int FFMS_GetFirstIndexedTrackOfType(FFMS_Index* Index, int TrackType, FFMS_ErrorInfo* ErrorInfo);

    [DllImport(Library.FFmpegSource2Dll, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int FFMS_GetNumTracks(FFMS_Index* Index);

    [DllImport(Library.FFmpegSource2Dll, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int FFMS_GetNumTracksI(FFMS_Indexer* Indexer);

    [DllImport(Library.FFmpegSource2Dll, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int FFMS_GetTrackType(FFMS_Track* T);

    [DllImport(Library.FFmpegSource2Dll, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int FFMS_GetTrackTypeI(FFMS_Indexer* Indexer, int Track);

    [DllImport(Library.FFmpegSource2Dll, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern FFMS_IndexErrorHandling FFMS_GetErrorHandling(FFMS_Index* Index);

    [DllImport(Library.FFmpegSource2Dll, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("const char *")]
    public static extern sbyte* FFMS_GetCodecNameI(FFMS_Indexer* Indexer, int Track);

    [DllImport(Library.FFmpegSource2Dll, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("const char *")]
    public static extern sbyte* FFMS_GetFormatNameI(FFMS_Indexer* Indexer);

    [DllImport(Library.FFmpegSource2Dll, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int FFMS_GetNumFrames(FFMS_Track* T);

    [DllImport(Library.FFmpegSource2Dll, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("const FFMS_FrameInfo *")]
    public static extern FFMS_FrameInfo* FFMS_GetFrameInfo(FFMS_Track* T, int Frame);

    [DllImport(Library.FFmpegSource2Dll, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern FFMS_Track* FFMS_GetTrackFromIndex(FFMS_Index* Index, int Track);

    [DllImport(Library.FFmpegSource2Dll, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern FFMS_Track* FFMS_GetTrackFromVideo(FFMS_VideoSource* V);

    [DllImport(Library.FFmpegSource2Dll, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern FFMS_Track* FFMS_GetTrackFromAudio(FFMS_AudioSource* A);

    [DllImport(Library.FFmpegSource2Dll, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("const FFMS_TrackTimeBase *")]
    public static extern FFMS_TrackTimeBase* FFMS_GetTimeBase(FFMS_Track* T);

    [DllImport(Library.FFmpegSource2Dll, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int FFMS_WriteTimecodes(FFMS_Track* T, [NativeTypeName("const char *")] sbyte* TimecodeFile, FFMS_ErrorInfo* ErrorInfo);

    [DllImport(Library.FFmpegSource2Dll, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern FFMS_Indexer* FFMS_CreateIndexer([NativeTypeName("const char *")] sbyte* SourceFile, FFMS_ErrorInfo* ErrorInfo);

    [DllImport(Library.FFmpegSource2Dll, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern FFMS_Indexer* FFMS_CreateIndexer2([NativeTypeName("const char *")] sbyte* SourceFile, [NativeTypeName("const FFMS_KeyValuePair *")] FFMS_KeyValuePair* DemuxerOptions, int NumOptions, FFMS_ErrorInfo* ErrorInfo);

    [DllImport(Library.FFmpegSource2Dll, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern void FFMS_TrackIndexSettings(FFMS_Indexer* Indexer, int Track, int Index, int param3);

    [DllImport(Library.FFmpegSource2Dll, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern void FFMS_TrackTypeIndexSettings(FFMS_Indexer* Indexer, int TrackType, int Index, int param3);

    [DllImport(Library.FFmpegSource2Dll, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern void FFMS_SetProgressCallback(FFMS_Indexer* Indexer, [NativeTypeName("TIndexCallback")] delegate* unmanaged[Cdecl]<long, long, void*, int> IC, void* ICPrivate);

    [DllImport(Library.FFmpegSource2Dll, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern FFMS_Index* FFMS_DoIndexing2(FFMS_Indexer* Indexer, int ErrorHandling, FFMS_ErrorInfo* ErrorInfo);

    [DllImport(Library.FFmpegSource2Dll, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern void FFMS_CancelIndexing(FFMS_Indexer* Indexer);

    [DllImport(Library.FFmpegSource2Dll, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern FFMS_Index* FFMS_ReadIndex([NativeTypeName("const char *")] sbyte* IndexFile, FFMS_ErrorInfo* ErrorInfo);

    [DllImport(Library.FFmpegSource2Dll, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern FFMS_Index* FFMS_ReadIndexFromBuffer([NativeTypeName("const uint8_t *")] byte* Buffer, [NativeTypeName("size_t")] nuint Size, FFMS_ErrorInfo* ErrorInfo);

    [DllImport(Library.FFmpegSource2Dll, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int FFMS_IndexBelongsToFile(FFMS_Index* Index, [NativeTypeName("const char *")] sbyte* SourceFile, FFMS_ErrorInfo* ErrorInfo);

    [DllImport(Library.FFmpegSource2Dll, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int FFMS_WriteIndex([NativeTypeName("const char *")] sbyte* IndexFile, FFMS_Index* Index, FFMS_ErrorInfo* ErrorInfo);

    [DllImport(Library.FFmpegSource2Dll, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int FFMS_WriteIndexToBuffer([NativeTypeName("uint8_t **")] byte** BufferPtr, [NativeTypeName("size_t *")] nuint* Size, FFMS_Index* Index, FFMS_ErrorInfo* ErrorInfo);

    [DllImport(Library.FFmpegSource2Dll, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern void FFMS_FreeIndexBuffer([NativeTypeName("uint8_t **")] byte** BufferPtr);

    [DllImport(Library.FFmpegSource2Dll, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int FFMS_GetPixFmt([NativeTypeName("const char *")] sbyte* Name);
}