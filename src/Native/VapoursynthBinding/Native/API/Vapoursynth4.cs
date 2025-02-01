using static Mobsub.Native.VapoursynthBinding.Native.API.VSColorFamily;
using static Mobsub.Native.VapoursynthBinding.Native.API.VSSampleType;

// ReSharper disable InconsistentNaming

namespace Mobsub.Native.VapoursynthBinding.Native.API
{
    public partial struct VSFrame
    {
    }

    public partial struct VSNode
    {
    }

    public partial struct VSCore
    {
    }

    public partial struct VSPlugin
    {
    }

    public partial struct VSPluginFunction
    {
    }

    public partial struct VSFunction
    {
    }

    public partial struct VSMap
    {
    }

    public partial struct VSLogHandle
    {
    }

    public partial struct VSFrameContext
    {
    }

    //public enum VSColorFamily : int
    //{
    //    cfUndefined = 0,
    //    cfGray = 1,
    //    cfRGB = 2,
    //    cfYUV = 3,
    //}

    public enum VSSampleType
    {
        stInteger = 0,
        stFloat = 1,
    }

    public enum VSPresetFormat : int
    {
        pfNone = 0,
        pfGray8 = ((cfGray << 28) | (stInteger << 24) | (8 << 16) | (0 << 8) | (0 << 0)),
        pfGray9 = ((cfGray << 28) | (stInteger << 24) | (9 << 16) | (0 << 8) | (0 << 0)),
        pfGray10 = ((cfGray << 28) | (stInteger << 24) | (10 << 16) | (0 << 8) | (0 << 0)),
        pfGray12 = ((cfGray << 28) | (stInteger << 24) | (12 << 16) | (0 << 8) | (0 << 0)),
        pfGray14 = ((cfGray << 28) | (stInteger << 24) | (14 << 16) | (0 << 8) | (0 << 0)),
        pfGray16 = ((cfGray << 28) | (stInteger << 24) | (16 << 16) | (0 << 8) | (0 << 0)),
        pfGray32 = ((cfGray << 28) | (stInteger << 24) | (32 << 16) | (0 << 8) | (0 << 0)),
        pfGrayH = ((cfGray << 28) | (stFloat << 24) | (16 << 16) | (0 << 8) | (0 << 0)),
        pfGrayS = ((cfGray << 28) | (stFloat << 24) | (32 << 16) | (0 << 8) | (0 << 0)),
        pfYUV410P8 = ((cfYUV << 28) | (stInteger << 24) | (8 << 16) | (2 << 8) | (2 << 0)),
        pfYUV411P8 = ((cfYUV << 28) | (stInteger << 24) | (8 << 16) | (2 << 8) | (0 << 0)),
        pfYUV440P8 = ((cfYUV << 28) | (stInteger << 24) | (8 << 16) | (0 << 8) | (1 << 0)),
        pfYUV420P8 = ((cfYUV << 28) | (stInteger << 24) | (8 << 16) | (1 << 8) | (1 << 0)),
        pfYUV422P8 = ((cfYUV << 28) | (stInteger << 24) | (8 << 16) | (1 << 8) | (0 << 0)),
        pfYUV444P8 = ((cfYUV << 28) | (stInteger << 24) | (8 << 16) | (0 << 8) | (0 << 0)),
        pfYUV420P9 = ((cfYUV << 28) | (stInteger << 24) | (9 << 16) | (1 << 8) | (1 << 0)),
        pfYUV422P9 = ((cfYUV << 28) | (stInteger << 24) | (9 << 16) | (1 << 8) | (0 << 0)),
        pfYUV444P9 = ((cfYUV << 28) | (stInteger << 24) | (9 << 16) | (0 << 8) | (0 << 0)),
        pfYUV420P10 = ((cfYUV << 28) | (stInteger << 24) | (10 << 16) | (1 << 8) | (1 << 0)),
        pfYUV422P10 = ((cfYUV << 28) | (stInteger << 24) | (10 << 16) | (1 << 8) | (0 << 0)),
        pfYUV444P10 = ((cfYUV << 28) | (stInteger << 24) | (10 << 16) | (0 << 8) | (0 << 0)),
        pfYUV420P12 = ((cfYUV << 28) | (stInteger << 24) | (12 << 16) | (1 << 8) | (1 << 0)),
        pfYUV422P12 = ((cfYUV << 28) | (stInteger << 24) | (12 << 16) | (1 << 8) | (0 << 0)),
        pfYUV444P12 = ((cfYUV << 28) | (stInteger << 24) | (12 << 16) | (0 << 8) | (0 << 0)),
        pfYUV420P14 = ((cfYUV << 28) | (stInteger << 24) | (14 << 16) | (1 << 8) | (1 << 0)),
        pfYUV422P14 = ((cfYUV << 28) | (stInteger << 24) | (14 << 16) | (1 << 8) | (0 << 0)),
        pfYUV444P14 = ((cfYUV << 28) | (stInteger << 24) | (14 << 16) | (0 << 8) | (0 << 0)),
        pfYUV420P16 = ((cfYUV << 28) | (stInteger << 24) | (16 << 16) | (1 << 8) | (1 << 0)),
        pfYUV422P16 = ((cfYUV << 28) | (stInteger << 24) | (16 << 16) | (1 << 8) | (0 << 0)),
        pfYUV444P16 = ((cfYUV << 28) | (stInteger << 24) | (16 << 16) | (0 << 8) | (0 << 0)),
        pfYUV444PH = ((cfYUV << 28) | (stFloat << 24) | (16 << 16) | (0 << 8) | (0 << 0)),
        pfYUV444PS = ((cfYUV << 28) | (stFloat << 24) | (32 << 16) | (0 << 8) | (0 << 0)),
        pfRGB24 = ((cfRGB << 28) | (stInteger << 24) | (8 << 16) | (0 << 8) | (0 << 0)),
        pfRGB27 = ((cfRGB << 28) | (stInteger << 24) | (9 << 16) | (0 << 8) | (0 << 0)),
        pfRGB30 = ((cfRGB << 28) | (stInteger << 24) | (10 << 16) | (0 << 8) | (0 << 0)),
        pfRGB36 = ((cfRGB << 28) | (stInteger << 24) | (12 << 16) | (0 << 8) | (0 << 0)),
        pfRGB42 = ((cfRGB << 28) | (stInteger << 24) | (14 << 16) | (0 << 8) | (0 << 0)),
        pfRGB48 = ((cfRGB << 28) | (stInteger << 24) | (16 << 16) | (0 << 8) | (0 << 0)),
        pfRGBH = ((cfRGB << 28) | (stFloat << 24) | (16 << 16) | (0 << 8) | (0 << 0)),
        pfRGBS = ((cfRGB << 28) | (stFloat << 24) | (32 << 16) | (0 << 8) | (0 << 0)),
    }

    public enum VSFilterMode
    {
        fmParallel = 0,
        fmParallelRequests = 1,
        fmUnordered = 2,
        fmFrameState = 3,
    }

    public enum VSMediaType
    {
        mtVideo = 1,
        mtAudio = 2,
    }

    public partial struct VSVideoFormat
    {
        public VSColorFamily colorFamily;

        public VSSampleType sampleType;

        public int bitsPerSample;

        public int bytesPerSample;

        public int subSamplingW;

        public int subSamplingH;

        public int numPlanes;
    }

    public enum VSAudioChannels
    {
        acFrontLeft = 0,
        acFrontRight = 1,
        acFrontCenter = 2,
        acLowFrequency = 3,
        acBackLeft = 4,
        acBackRight = 5,
        acFrontLeftOFCenter = 6,
        acFrontRightOFCenter = 7,
        acBackCenter = 8,
        acSideLeft = 9,
        acSideRight = 10,
        acTopCenter = 11,
        acTopFrontLeft = 12,
        acTopFrontCenter = 13,
        acTopFrontRight = 14,
        acTopBackLeft = 15,
        acTopBackCenter = 16,
        acTopBackRight = 17,
        acStereoLeft = 29,
        acStereoRight = 30,
        acWideLeft = 31,
        acWideRight = 32,
        acSurroundDirectLeft = 33,
        acSurroundDirectRight = 34,
        acLowFrequency2 = 35,
    }

    public partial struct VSAudioFormat
    {
        public int sampleType;

        public int bitsPerSample;

        public int bytesPerSample;

        public int numChannels;

        [NativeTypeName("uint64_t")]
        public ulong channelLayout;
    }

    public enum VSPropertyType : int
    {
        ptUnset = 0,
        ptInt = 1,
        ptFloat = 2,
        ptData = 3,
        ptFunction = 4,
        ptVideoNode = 5,
        ptAudioNode = 6,
        ptVideoFrame = 7,
        ptAudioFrame = 8,
    }

    public enum VSMapPropertyError : int
    {
        peSuccess = 0,
        peUnset = 1,
        peType = 2,
        peIndex = 4,
        peError = 3,
    }

    public enum VSMapAppendMode
    {
        maReplace = 0,
        maAppend = 1,
    }

    public unsafe partial struct VSCoreInfo
    {
        [NativeTypeName("const char *")]
        public sbyte* versionString;

        public int core;

        public int api;

        public int numThreads;

        [NativeTypeName("int64_t")]
        public long maxFramebufferSize;

        [NativeTypeName("int64_t")]
        public long usedFramebufferSize;
    }

    public partial struct VSVideoInfo
    {
        public VSVideoFormat format;

        [NativeTypeName("int64_t")]
        public long fpsNum;

        [NativeTypeName("int64_t")]
        public long fpsDen;

        public int width;

        public int height;

        public int numFrames;
    }

    public partial struct VSAudioInfo
    {
        public VSAudioFormat format;

        public int sampleRate;

        [NativeTypeName("int64_t")]
        public long numSamples;

        public int numFrames;
    }

    public enum VSActivationReason
    {
        arInitial = 0,
        arAllFramesReady = 1,
        arError = -1,
    }

    public enum VSMessageType
    {
        mtDebug = 0,
        mtInformation = 1,
        mtWarning = 2,
        mtCritical = 3,
        mtFatal = 4,
    }

    public enum VSCoreCreationFlags : int
    {
        Default = 0,
        ccfEnableGraphInspection = 1,
        ccfDisableAutoLoading = 2,
        ccfDisableLibraryUnloading = 4,
    }

    public enum VSPluginConfigFlags
    {
        pcModifiable = 1,
    }

    public enum VSDataTypeHint : int
    {
        dtUnknown = -1,
        dtBinary = 0,
        dtUtf8 = 1,
    }

    public enum VSRequestPattern
    {
        rpGeneral = 0,
        rpNoFrameReuse = 1,
        rpStrictSpatial = 2,
    }

    public unsafe partial struct VSPLUGINAPI
    {
        [NativeTypeName("int (*)(void)")]
        public delegate* unmanaged[Cdecl]<int> getAPIVersion;

        [NativeTypeName("int (*)(const char *, const char *, const char *, int, int, int, VSPlugin *)")]
        public delegate* unmanaged[Cdecl]<sbyte*, sbyte*, sbyte*, int, int, int, VSPlugin*, int> configPlugin;

        [NativeTypeName("int (*)(const char *, const char *, const char *, VSPublicFunction, void *, VSPlugin *)")]
        public delegate* unmanaged[Cdecl]<sbyte*, sbyte*, sbyte*, delegate* unmanaged[Cdecl]<VSMap*, VSMap*, void*, VSCore*, VSAPI*, void>, void*, VSPlugin*, int> registerFunction;
    }

    public unsafe partial struct VSFilterDependency
    {
        public VSNode* source;

        public int requestPattern;
    }

    public unsafe partial struct VSAPI
    {
        [NativeTypeName("void (*)(VSMap *, const char *, const VSVideoInfo *, VSFilterGetFrame, VSFilterFree, int, const VSFilterDependency *, int, void *, VSCore *)")]
        public delegate* unmanaged[Cdecl]<VSMap*, sbyte*, VSVideoInfo*, delegate* unmanaged[Cdecl]<int, int, void*, void**, VSFrameContext*, VSCore*, VSAPI*, VSFrame*>, delegate* unmanaged[Cdecl]<void*, VSCore*, VSAPI*, void>, int, VSFilterDependency*, int, void*, VSCore*, void> createVideoFilter;

        [NativeTypeName("VSNode *(*)(const char *, const VSVideoInfo *, VSFilterGetFrame, VSFilterFree, int, const VSFilterDependency *, int, void *, VSCore *)")]
        public delegate* unmanaged[Cdecl]<sbyte*, VSVideoInfo*, delegate* unmanaged[Cdecl]<int, int, void*, void**, VSFrameContext*, VSCore*, VSAPI*, VSFrame*>, delegate* unmanaged[Cdecl]<void*, VSCore*, VSAPI*, void>, int, VSFilterDependency*, int, void*, VSCore*, VSNode*> createVideoFilter2;

        [NativeTypeName("void (*)(VSMap *, const char *, const VSAudioInfo *, VSFilterGetFrame, VSFilterFree, int, const VSFilterDependency *, int, void *, VSCore *)")]
        public delegate* unmanaged[Cdecl]<VSMap*, sbyte*, VSAudioInfo*, delegate* unmanaged[Cdecl]<int, int, void*, void**, VSFrameContext*, VSCore*, VSAPI*, VSFrame*>, delegate* unmanaged[Cdecl]<void*, VSCore*, VSAPI*, void>, int, VSFilterDependency*, int, void*, VSCore*, void> createAudioFilter;

        [NativeTypeName("VSNode *(*)(const char *, const VSAudioInfo *, VSFilterGetFrame, VSFilterFree, int, const VSFilterDependency *, int, void *, VSCore *)")]
        public delegate* unmanaged[Cdecl]<sbyte*, VSAudioInfo*, delegate* unmanaged[Cdecl]<int, int, void*, void**, VSFrameContext*, VSCore*, VSAPI*, VSFrame*>, delegate* unmanaged[Cdecl]<void*, VSCore*, VSAPI*, void>, int, VSFilterDependency*, int, void*, VSCore*, VSNode*> createAudioFilter2;

        [NativeTypeName("int (*)(VSNode *)")]
        public delegate* unmanaged[Cdecl]<VSNode*, int> setLinearFilter;

        [NativeTypeName("void (*)(VSNode *, int)")]
        public delegate* unmanaged[Cdecl]<VSNode*, int, void> setCacheMode;

        [NativeTypeName("void (*)(VSNode *, int, int, int)")]
        public delegate* unmanaged[Cdecl]<VSNode*, int, int, int, void> setCacheOptions;

        [NativeTypeName("void (*)(VSNode *)")]
        public delegate* unmanaged[Cdecl]<VSNode*, void> freeNode;

        [NativeTypeName("VSNode *(*)(VSNode *)")]
        public delegate* unmanaged[Cdecl]<VSNode*, VSNode*> addNodeRef;

        [NativeTypeName("int (*)(VSNode *)")]
        public delegate* unmanaged[Cdecl]<VSNode*, int> getNodeType;

        [NativeTypeName("const VSVideoInfo *(*)(VSNode *)")]
        public delegate* unmanaged[Cdecl]<VSNode*, VSVideoInfo*> getVideoInfo;

        [NativeTypeName("const VSAudioInfo *(*)(VSNode *)")]
        public delegate* unmanaged[Cdecl]<VSNode*, VSAudioInfo*> getAudioInfo;

        [NativeTypeName("VSFrame *(*)(const VSVideoFormat *, int, int, const VSFrame *, VSCore *)")]
        public delegate* unmanaged[Cdecl]<VSVideoFormat*, int, int, VSFrame*, VSCore*, VSFrame*> newVideoFrame;

        [NativeTypeName("VSFrame *(*)(const VSVideoFormat *, int, int, const VSFrame **, const int *, const VSFrame *, VSCore *)")]
        public delegate* unmanaged[Cdecl]<VSVideoFormat*, int, int, VSFrame**, int*, VSFrame*, VSCore*, VSFrame*> newVideoFrame2;

        [NativeTypeName("VSFrame *(*)(const VSAudioFormat *, int, const VSFrame *, VSCore *)")]
        public delegate* unmanaged[Cdecl]<VSAudioFormat*, int, VSFrame*, VSCore*, VSFrame*> newAudioFrame;

        [NativeTypeName("VSFrame *(*)(const VSAudioFormat *, int, const VSFrame **, const int *, const VSFrame *, VSCore *)")]
        public delegate* unmanaged[Cdecl]<VSAudioFormat*, int, VSFrame**, int*, VSFrame*, VSCore*, VSFrame*> newAudioFrame2;

        [NativeTypeName("void (*)(const VSFrame *)")]
        public delegate* unmanaged[Cdecl]<VSFrame*, void> freeFrame;

        [NativeTypeName("const VSFrame *(*)(const VSFrame *)")]
        public delegate* unmanaged[Cdecl]<VSFrame*, VSFrame*> addFrameRef;

        [NativeTypeName("VSFrame *(*)(const VSFrame *, VSCore *)")]
        public delegate* unmanaged[Cdecl]<VSFrame*, VSCore*, VSFrame*> copyFrame;

        [NativeTypeName("const VSMap *(*)(const VSFrame *)")]
        public delegate* unmanaged[Cdecl]<VSFrame*, VSMap*> getFramePropertiesRO;

        [NativeTypeName("VSMap *(*)(VSFrame *)")]
        public delegate* unmanaged[Cdecl]<VSFrame*, VSMap*> getFramePropertiesRW;

        [NativeTypeName("ptrdiff_t (*)(const VSFrame *, int)")]
        public delegate* unmanaged[Cdecl]<VSFrame*, int, nint> getStride;

        [NativeTypeName("const uint8_t *(*)(const VSFrame *, int)")]
        public delegate* unmanaged[Cdecl]<VSFrame*, int, byte*> getReadPtr;

        [NativeTypeName("uint8_t *(*)(VSFrame *, int)")]
        public delegate* unmanaged[Cdecl]<VSFrame*, int, byte*> getWritePtr;

        [NativeTypeName("const VSVideoFormat *(*)(const VSFrame *)")]
        public delegate* unmanaged[Cdecl]<VSFrame*, VSVideoFormat*> getVideoFrameFormat;

        [NativeTypeName("const VSAudioFormat *(*)(const VSFrame *)")]
        public delegate* unmanaged[Cdecl]<VSFrame*, VSAudioFormat*> getAudioFrameFormat;

        [NativeTypeName("int (*)(const VSFrame *)")]
        public delegate* unmanaged[Cdecl]<VSFrame*, int> getFrameType;

        [NativeTypeName("int (*)(const VSFrame *, int)")]
        public delegate* unmanaged[Cdecl]<VSFrame*, int, int> getFrameWidth;

        [NativeTypeName("int (*)(const VSFrame *, int)")]
        public delegate* unmanaged[Cdecl]<VSFrame*, int, int> getFrameHeight;

        [NativeTypeName("int (*)(const VSFrame *)")]
        public delegate* unmanaged[Cdecl]<VSFrame*, int> getFrameLength;

        [NativeTypeName("int (*)(const VSVideoFormat *, char *)")]
        public delegate* unmanaged[Cdecl]<VSVideoFormat*, sbyte*, int> getVideoFormatName;

        [NativeTypeName("int (*)(const VSAudioFormat *, char *)")]
        public delegate* unmanaged[Cdecl]<VSAudioFormat*, sbyte*, int> getAudioFormatName;

        [NativeTypeName("int (*)(VSVideoFormat *, int, int, int, int, int, VSCore *)")]
        public delegate* unmanaged[Cdecl]<VSVideoFormat*, int, int, int, int, int, VSCore*, int> queryVideoFormat;

        [NativeTypeName("int (*)(VSAudioFormat *, int, int, uint64_t, VSCore *)")]
        public delegate* unmanaged[Cdecl]<VSAudioFormat*, int, int, ulong, VSCore*, int> queryAudioFormat;

        [NativeTypeName("uint32_t (*)(int, int, int, int, int, VSCore *)")]
        public delegate* unmanaged[Cdecl]<int, int, int, int, int, VSCore*, uint> queryVideoFormatID;

        [NativeTypeName("int (*)(VSVideoFormat *, uint32_t, VSCore *)")]
        public delegate* unmanaged[Cdecl]<VSVideoFormat*, uint, VSCore*, int> getVideoFormatByID;

        [NativeTypeName("const VSFrame *(*)(int, VSNode *, char *, int)")]
        public delegate* unmanaged[Cdecl]<int, VSNode*, sbyte*, int, VSFrame*> getFrame;

        [NativeTypeName("void (*)(int, VSNode *, VSFrameDoneCallback, void *)")]
        public delegate* unmanaged[Cdecl]<int, VSNode*, delegate* unmanaged[Cdecl]<void*, VSFrame*, int, VSNode*, sbyte*, void>, void*, void> getFrameAsync;
        
        [NativeTypeName("const VSFrame *(*)(int, VSNode *, VSFrameContext *)")]
        public delegate* unmanaged[Cdecl]<int, VSNode*, VSFrameContext*, VSFrame*> getFrameFilter;

        [NativeTypeName("void (*)(int, VSNode *, VSFrameContext *)")]
        public delegate* unmanaged[Cdecl]<int, VSNode*, VSFrameContext*, void> requestFrameFilter;

        [NativeTypeName("void (*)(VSNode *, int, VSFrameContext *)")]
        public delegate* unmanaged[Cdecl]<VSNode*, int, VSFrameContext*, void> releaseFrameEarly;

        [NativeTypeName("void (*)(const VSFrame *, int, VSFrameContext *)")]
        public delegate* unmanaged[Cdecl]<VSFrame*, int, VSFrameContext*, void> cacheFrame;

        [NativeTypeName("void (*)(const char *, VSFrameContext *)")]
        public delegate* unmanaged[Cdecl]<sbyte*, VSFrameContext*, void> setFilterError;

        [NativeTypeName("VSFunction *(*)(VSPublicFunction, void *, VSFreeFunctionData, VSCore *)")]
        public delegate* unmanaged[Cdecl]<delegate* unmanaged[Cdecl]<VSMap*, VSMap*, void*, VSCore*, VSAPI*, void>, void*, delegate* unmanaged[Cdecl]<void*, void>, VSCore*, VSFunction*> createFunction;

        [NativeTypeName("void (*)(VSFunction *)")]
        public delegate* unmanaged[Cdecl]<VSFunction*, void> freeFunction;

        [NativeTypeName("VSFunction *(*)(VSFunction *)")]
        public delegate* unmanaged[Cdecl]<VSFunction*, VSFunction*> addFunctionRef;

        [NativeTypeName("void (*)(VSFunction *, const VSMap *, VSMap *)")]
        public delegate* unmanaged[Cdecl]<VSFunction*, VSMap*, VSMap*, void> callFunction;

        [NativeTypeName("VSMap *(*)(void)")]
        public delegate* unmanaged[Cdecl]<VSMap*> createMap;

        [NativeTypeName("void (*)(VSMap *)")]
        public delegate* unmanaged[Cdecl]<VSMap*, void> freeMap;

        [NativeTypeName("void (*)(VSMap *)")]
        public delegate* unmanaged[Cdecl]<VSMap*, void> clearMap;

        [NativeTypeName("void (*)(const VSMap *, VSMap *)")]
        public delegate* unmanaged[Cdecl]<VSMap*, VSMap*, void> copyMap;

        [NativeTypeName("void (*)(VSMap *, const char *)")]
        public delegate* unmanaged[Cdecl]<VSMap*, sbyte*, void> mapSetError;

        [NativeTypeName("const char *(*)(const VSMap *)")]
        public delegate* unmanaged[Cdecl]<VSMap*, sbyte*> mapGetError;

        [NativeTypeName("int (*)(const VSMap *)")]
        public delegate* unmanaged[Cdecl]<VSMap*, int> mapNumKeys;

        [NativeTypeName("const char *(*)(const VSMap *, int)")]
        public delegate* unmanaged[Cdecl]<VSMap*, int, sbyte*> mapGetKey;

        [NativeTypeName("int (*)(VSMap *, const char *)")]
        public delegate* unmanaged[Cdecl]<VSMap*, sbyte*, int> mapDeleteKey;

        [NativeTypeName("int (*)(const VSMap *, const char *)")]
        public delegate* unmanaged[Cdecl]<VSMap*, sbyte*, int> mapNumElements;

        [NativeTypeName("int (*)(const VSMap *, const char *)")]
        public delegate* unmanaged[Cdecl]<VSMap*, sbyte*, VSPropertyType> mapGetType;

        [NativeTypeName("int (*)(VSMap *, const char *, int)")]
        public delegate* unmanaged[Cdecl]<VSMap*, sbyte*, int, int> mapSetEmpty;

        [NativeTypeName("int64_t (*)(const VSMap *, const char *, int, int *)")]
        public delegate* unmanaged[Cdecl]<VSMap*, sbyte*, int, int*, long> mapGetInt;

        [NativeTypeName("int (*)(const VSMap *, const char *, int, int *)")]
        public delegate* unmanaged[Cdecl]<VSMap*, sbyte*, int, int*, int> mapGetIntSaturated;

        [NativeTypeName("const int64_t *(*)(const VSMap *, const char *, int *)")]
        public delegate* unmanaged[Cdecl]<VSMap*, sbyte*, int*, long*> mapGetIntArray;

        [NativeTypeName("int (*)(VSMap *, const char *, int64_t, int)")]
        public delegate* unmanaged[Cdecl]<VSMap*, sbyte*, long, int, int> mapSetInt;

        [NativeTypeName("int (*)(VSMap *, const char *, const int64_t *, int)")]
        public delegate* unmanaged[Cdecl]<VSMap*, sbyte*, long*, int, int> mapSetIntArray;

        [NativeTypeName("double (*)(const VSMap *, const char *, int, int *)")]
        public delegate* unmanaged[Cdecl]<VSMap*, sbyte*, int, int*, double> mapGetFloat;

        [NativeTypeName("float (*)(const VSMap *, const char *, int, int *)")]
        public delegate* unmanaged[Cdecl]<VSMap*, sbyte*, int, int*, float> mapGetFloatSaturated;

        [NativeTypeName("const double *(*)(const VSMap *, const char *, int *)")]
        public delegate* unmanaged[Cdecl]<VSMap*, sbyte*, int*, double*> mapGetFloatArray;

        [NativeTypeName("int (*)(VSMap *, const char *, double, int)")]
        public delegate* unmanaged[Cdecl]<VSMap*, sbyte*, double, int, int> mapSetFloat;

        [NativeTypeName("int (*)(VSMap *, const char *, const double *, int)")]
        public delegate* unmanaged[Cdecl]<VSMap*, sbyte*, double*, int, int> mapSetFloatArray;

        [NativeTypeName("const char *(*)(const VSMap *, const char *, int, int *)")]
        public delegate* unmanaged[Cdecl]<VSMap*, sbyte*, int, int*, sbyte*> mapGetData;

        [NativeTypeName("int (*)(const VSMap *, const char *, int, int *)")]
        public delegate* unmanaged[Cdecl]<VSMap*, sbyte*, int, int*, int> mapGetDataSize;

        [NativeTypeName("int (*)(const VSMap *, const char *, int, int *)")]
        public delegate* unmanaged[Cdecl]<VSMap*, sbyte*, int, int*, int> mapGetDataTypeHint;

        [NativeTypeName("int (*)(VSMap *, const char *, const char *, int, int, int)")]
        public delegate* unmanaged[Cdecl]<VSMap*, sbyte*, sbyte*, int, int, int, int> mapSetData;

        [NativeTypeName("VSNode *(*)(const VSMap *, const char *, int, int *)")]
        public delegate* unmanaged[Cdecl]<VSMap*, sbyte*, int, int*, VSNode*> mapGetNode;

        [NativeTypeName("int (*)(VSMap *, const char *, VSNode *, int)")]
        public delegate* unmanaged[Cdecl]<VSMap*, sbyte*, VSNode*, int, int> mapSetNode;

        [NativeTypeName("int (*)(VSMap *, const char *, VSNode *, int)")]
        public delegate* unmanaged[Cdecl]<VSMap*, sbyte*, VSNode*, int, int> mapConsumeNode;

        [NativeTypeName("const VSFrame *(*)(const VSMap *, const char *, int, int *)")]
        public delegate* unmanaged[Cdecl]<VSMap*, sbyte*, int, int*, VSFrame*> mapGetFrame;

        [NativeTypeName("int (*)(VSMap *, const char *, const VSFrame *, int)")]
        public delegate* unmanaged[Cdecl]<VSMap*, sbyte*, VSFrame*, int, int> mapSetFrame;

        [NativeTypeName("int (*)(VSMap *, const char *, const VSFrame *, int)")]
        public delegate* unmanaged[Cdecl]<VSMap*, sbyte*, VSFrame*, int, int> mapConsumeFrame;

        [NativeTypeName("VSFunction *(*)(const VSMap *, const char *, int, int *)")]
        public delegate* unmanaged[Cdecl]<VSMap*, sbyte*, int, int*, VSFunction*> mapGetFunction;

        [NativeTypeName("int (*)(VSMap *, const char *, VSFunction *, int)")]
        public delegate* unmanaged[Cdecl]<VSMap*, sbyte*, VSFunction*, int, int> mapSetFunction;

        [NativeTypeName("int (*)(VSMap *, const char *, VSFunction *, int)")]
        public delegate* unmanaged[Cdecl]<VSMap*, sbyte*, VSFunction*, int, int> mapConsumeFunction;

        [NativeTypeName("int (*)(const char *, const char *, const char *, VSPublicFunction, void *, VSPlugin *)")]
        public delegate* unmanaged[Cdecl]<sbyte*, sbyte*, sbyte*, delegate* unmanaged[Cdecl]<VSMap*, VSMap*, void*, VSCore*, VSAPI*, void>, void*, VSPlugin*, int> registerFunction;

        [NativeTypeName("VSPlugin *(*)(const char *, VSCore *)")]
        public delegate* unmanaged[Cdecl]<sbyte*, VSCore*, VSPlugin*> getPluginByID;

        [NativeTypeName("VSPlugin *(*)(const char *, VSCore *)")]
        public delegate* unmanaged[Cdecl]<sbyte*, VSCore*, VSPlugin*> getPluginByNamespace;

        [NativeTypeName("VSPlugin *(*)(VSPlugin *, VSCore *)")]
        public delegate* unmanaged[Cdecl]<VSPlugin*, VSCore*, VSPlugin*> getNextPlugin;

        [NativeTypeName("const char *(*)(VSPlugin *)")]
        public delegate* unmanaged[Cdecl]<VSPlugin*, sbyte*> getPluginName;

        [NativeTypeName("const char *(*)(VSPlugin *)")]
        public delegate* unmanaged[Cdecl]<VSPlugin*, sbyte*> getPluginID;

        [NativeTypeName("const char *(*)(VSPlugin *)")]
        public delegate* unmanaged[Cdecl]<VSPlugin*, sbyte*> getPluginNamespace;

        [NativeTypeName("VSPluginFunction *(*)(VSPluginFunction *, VSPlugin *)")]
        public delegate* unmanaged[Cdecl]<VSPluginFunction*, VSPlugin*, VSPluginFunction*> getNextPluginFunction;

        [NativeTypeName("VSPluginFunction *(*)(const char *, VSPlugin *)")]
        public delegate* unmanaged[Cdecl]<sbyte*, VSPlugin*, VSPluginFunction*> getPluginFunctionByName;

        [NativeTypeName("const char *(*)(VSPluginFunction *)")]
        public delegate* unmanaged[Cdecl]<VSPluginFunction*, sbyte*> getPluginFunctionName;

        [NativeTypeName("const char *(*)(VSPluginFunction *)")]
        public delegate* unmanaged[Cdecl]<VSPluginFunction*, sbyte*> getPluginFunctionArguments;

        [NativeTypeName("const char *(*)(VSPluginFunction *)")]
        public delegate* unmanaged[Cdecl]<VSPluginFunction*, sbyte*> getPluginFunctionReturnType;

        [NativeTypeName("const char *(*)(const VSPlugin *)")]
        public delegate* unmanaged[Cdecl]<VSPlugin*, sbyte*> getPluginPath;

        [NativeTypeName("int (*)(const VSPlugin *)")]
        public delegate* unmanaged[Cdecl]<VSPlugin*, int> getPluginVersion;

        [NativeTypeName("VSMap *(*)(VSPlugin *, const char *, const VSMap *)")]
        public delegate* unmanaged[Cdecl]<VSPlugin*, sbyte*, VSMap*, VSMap*> invoke;

        [NativeTypeName("VSCore *(*)(int)")]
        public delegate* unmanaged[Cdecl]<int, VSCore*> createCore;

        [NativeTypeName("void (*)(VSCore *)")]
        public delegate* unmanaged[Cdecl]<VSCore*, void> freeCore;

        [NativeTypeName("int64_t (*)(int64_t, VSCore *)")]
        public delegate* unmanaged[Cdecl]<long, VSCore*, long> setMaxCacheSize;

        [NativeTypeName("int (*)(int, VSCore *)")]
        public delegate* unmanaged[Cdecl]<int, VSCore*, int> setThreadCount;

        [NativeTypeName("void (*)(VSCore *, VSCoreInfo *)")]
        public delegate* unmanaged[Cdecl]<VSCore*, VSCoreInfo*, void> getCoreInfo;

        [NativeTypeName("int (*)(void)")]
        public delegate* unmanaged[Cdecl]<int> getAPIVersion;

        [NativeTypeName("void (*)(int, const char *, VSCore *)")]
        public delegate* unmanaged[Cdecl]<int, sbyte*, VSCore*, void> logMessage;

        [NativeTypeName("VSLogHandle *(*)(VSLogHandler, VSLogHandlerFree, void *, VSCore *)")]
        public delegate* unmanaged[Cdecl]<delegate* unmanaged[Cdecl]<int, sbyte*, void*, void>, delegate* unmanaged[Cdecl]<void*, void>, void*, VSCore*, VSLogHandle*> addLogHandler;

        [NativeTypeName("int (*)(VSLogHandle *, VSCore *)")]
        public delegate* unmanaged[Cdecl]<VSLogHandle*, VSCore*, int> removeLogHandler;
    }
}
