using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Mobsub.Native.VapoursynthBinding.Native.API;
using static Mobsub.Native.VapoursynthBinding.Native.API.VSColorFamily;

// ReSharper disable InconsistentNaming

using VSNodeRef = Mobsub.Native.VapoursynthBinding.Native.API.VSNode;
using VSFrameRef = Mobsub.Native.VapoursynthBinding.Native.API.VSFrame;
using VSSampleType = Mobsub.Native.VapoursynthBinding.Native.API.VSSampleType;
using VSCoreInfo = Mobsub.Native.VapoursynthBinding.Native.API.VSCoreInfo;
using VSColorFamily = Mobsub.Native.VapoursynthBinding.Native.API.VSColorFamily;
using VSMap = Mobsub.Native.VapoursynthBinding.Native.API.VSMap;
using VSCore = Mobsub.Native.VapoursynthBinding.Native.API.VSCore;
using VSFuncRef = Mobsub.Native.VapoursynthBinding.Native.API.VSFunction;
using VSPlugin = Mobsub.Native.VapoursynthBinding.Native.API.VSPlugin;
using VSFrameContext = Mobsub.Native.VapoursynthBinding.Native.API.VSFrameContext;

namespace Mobsub.Native.VapoursynthBinding.Native.API.API3
{
    // public partial struct VSFrameRef
    // {
    // }

    // public partial struct VSNodeRef
    // {
    // }

    // public partial struct VSCore
    // {
    // }

    // public partial struct VSPlugin
    // {
    // }

    public partial struct VSNode3
    {
    }

    // public partial struct VSFuncRef
    // {
    // }

    // public partial struct VSMap
    // {
    // }

    // public partial struct VSFrameContext
    // {
    // }

    //public enum VSColorFamily : int
    //{
    //    cmGray = 1000000,
    //    cmRGB = 2000000,
    //    cmYUV = 3000000,
    //    cmYCoCg = 4000000,
    //    cmCompat = 9000000,
    //}
    
    // public enum VSSampleType : int
    // {
    //     stInteger = 0,
    //     stFloat = 1,
    // }

    public enum VSPresetFormat3 : int
    {
        pfNone = 0,
        pfGray8 = cmGray + 10,
        pfGray16,
        pfGrayH,
        pfGrayS,
        pfYUV420P8 = cmYUV + 10,
        pfYUV422P8,
        pfYUV444P8,
        pfYUV410P8,
        pfYUV411P8,
        pfYUV440P8,
        pfYUV420P9,
        pfYUV422P9,
        pfYUV444P9,
        pfYUV420P10,
        pfYUV422P10,
        pfYUV444P10,
        pfYUV420P16,
        pfYUV422P16,
        pfYUV444P16,
        pfYUV444PH,
        pfYUV444PS,
        pfYUV420P12,
        pfYUV422P12,
        pfYUV444P12,
        pfYUV420P14,
        pfYUV422P14,
        pfYUV444P14,
        pfRGB24 = cmRGB + 10,
        pfRGB27,
        pfRGB30,
        pfRGB48,
        pfRGBH,
        pfRGBS,
        pfCompatBGR32 = cmCompat + 10,
        pfCompatYUY2,
    }

    public enum VSPropertyType : int
    {
        ptUnset3 = (sbyte)'u',
        ptInt3 = (sbyte)'i',
        ptFloat3 = (sbyte)'f',
        ptData3 = (sbyte)'s',
        ptNode3 = (sbyte)'c',
        ptFrame3 = (sbyte)'v',
        ptFunction3 = (sbyte)'m',
    }

    public enum VSFilterMode3
    {
        fmParallel = 100,
        fmParallelRequests = 200,
        fmUnordered = 300,
        fmSerial = 400,
    }

    public partial struct VSFormat
    {
        [NativeTypeName("char[32]")]
        public _name_e__FixedBuffer name;

        public VSPresetFormat3 id;

        public VSColorFamily colorFamily;

        public VSSampleType sampleType;

        public int bitsPerSample;

        public int bytesPerSample;

        public int subSamplingW;

        public int subSamplingH;

        public int numPlanes;

        [InlineArray(32)]
        public partial struct _name_e__FixedBuffer
        {
            public sbyte e0;
        }
    }

    public enum VSNodeFlags
    {
        nfNoCache = 1,
        nfIsCache = 2,
        nfMakeLinear = 4,
    }

    public enum VSPropTypes
    {
        ptUnset = 'u',
        ptInt = 'i',
        ptFloat = 'f',
        ptData = 's',
        ptNode = 'c',
        ptFrame = 'v',
        ptFunction = 'm',
    }

    public enum VSGetPropErrors : int
    {
        peUnset = 1,
        peType = 2,
        peIndex = 4,
    }

    public enum VSPropAppendMode
    {
        paReplace = 0,
        paAppend = 1,
        paTouch = 2,
    }

    //public unsafe partial struct VSCoreInfo
    //{
    //    [NativeTypeName("const char *")]
    //    public sbyte* versionString;

    //    public int core;

    //    public int api;

    //    public int numThreads;

    //    [NativeTypeName("int64_t")]
    //    public long maxFramebufferSize;

    //    [NativeTypeName("int64_t")]
    //    public long usedFramebufferSize;
    //}

    public unsafe partial struct VSVideoInfo3
    {
        [NativeTypeName("const VSFormat *")]
        public VSFormat* format;

        [NativeTypeName("int64_t")]
        public long fpsNum;

        [NativeTypeName("int64_t")]
        public long fpsDen;

        public int width;

        public int height;

        public int numFrames;

        public int flags;
    }

    public enum VSActivationReason3
    {
        arInitial = 0,
        arFrameReady = 1,
        arAllFramesReady = 2,
        arError = -1,
    }

    public enum VSMessageType3
    {
        mtDebug = 0,
        mtWarning = 1,
        mtCritical = 2,
        mtFatal = 3,
    }

    public unsafe partial struct VSAPI
    {
        [NativeTypeName("VSCore *(*)(int)")]
        public delegate* unmanaged[Cdecl]<int, VSCore*> createCore;

        [NativeTypeName("void (*)(VSCore *)")]
        public delegate* unmanaged[Cdecl]<VSCore*, void> freeCore;

        [Obsolete("Please use getCoreInfo2")]
        [NativeTypeName("const VSCoreInfo *(*)(VSCore *)")]
        public delegate* unmanaged[Cdecl]<VSCore*, VSCoreInfo*> getCoreInfo;

        [NativeTypeName("const VSFrameRef *(*)(const VSFrameRef *)")]
        public delegate* unmanaged[Cdecl]<VSFrame*, VSFrame*> cloneFrameRef;

        [NativeTypeName("VSNodeRef *(*)(VSNodeRef *)")]
        public delegate* unmanaged[Cdecl]<VSNode*, VSNode*> cloneNodeRef;

        [NativeTypeName("VSFuncRef *(*)(VSFuncRef *)")]
        public delegate* unmanaged[Cdecl]<VSFunction*, VSFunction*> cloneFuncRef;

        [NativeTypeName("void (*)(const VSFrameRef *)")]
        public delegate* unmanaged[Cdecl]<VSFrame*, void> freeFrame;

        [NativeTypeName("void (*)(VSNodeRef *)")]
        public delegate* unmanaged[Cdecl]<VSNode*, void> freeNode;

        [NativeTypeName("void (*)(VSFuncRef *)")]
        public delegate* unmanaged[Cdecl]<VSFunction*, void> freeFunc;

        [NativeTypeName("VSFrameRef *(*)(const VSFormat *, int, int, const VSFrameRef *, VSCore *)")]
        public delegate* unmanaged[Cdecl]<VSFormat*, int, int, VSFrame*, VSCore*, VSFrame*> newVideoFrame;

        [NativeTypeName("VSFrameRef *(*)(const VSFrameRef *, VSCore *)")]
        public delegate* unmanaged[Cdecl]<VSFrame*, VSCore*, VSFrame*> copyFrame;

        [NativeTypeName("void (*)(const VSFrameRef *, VSFrameRef *, VSCore *)")]
        public delegate* unmanaged[Cdecl]<VSFrame*, VSFrame*, VSCore*, void> copyFrameProps;

        [NativeTypeName("void (*)(const char *, const char *, VSPublicFunction, void *, VSPlugin *)")]
        public delegate* unmanaged[Cdecl]<sbyte*, sbyte*, delegate* unmanaged[Cdecl]<VSMap*, VSMap*, void*, VSCore*, VSAPI*, void>, void*, VSPlugin*, void> registerFunction3;

        [NativeTypeName("VSPlugin *(*)(const char *, VSCore *)")]
        public delegate* unmanaged[Cdecl]<sbyte*, VSCore*, VSPlugin*> getPluginById;

        [NativeTypeName("VSPlugin *(*)(const char *, VSCore *)")]
        public delegate* unmanaged[Cdecl]<sbyte*, VSCore*, VSPlugin*> getPluginByNs;

        [NativeTypeName("VSMap *(*)(VSCore *)")]
        public delegate* unmanaged[Cdecl]<VSCore*, VSMap*> getPlugins;

        [NativeTypeName("VSMap *(*)(VSPlugin *)")]
        public delegate* unmanaged[Cdecl]<VSPlugin*, VSMap*> getFunctions;

        [NativeTypeName("void (*)(const VSMap *, VSMap *, const char *, VSFilterInit, VSFilterGetFrame, VSFilterFree, int, int, void *, VSCore *)")]
        public delegate* unmanaged[Cdecl]<VSMap*, VSMap*, sbyte*, delegate* unmanaged[Cdecl]<VSMap*, VSMap*, void**, VSNode3*, VSCore*, VSAPI*, void>, delegate* unmanaged[Cdecl]<int, int, void**, void**, VSFrameContext*, VSCore*, VSAPI*, VSFrame*>, delegate* unmanaged[Cdecl]<void*, VSCore*, VSAPI*, void>, int, int, void*, VSCore*, void> createFilter;

        [NativeTypeName("void (*)(VSMap *, const char *)")]
        public delegate* unmanaged[Cdecl]<VSMap*, sbyte*, void> setError;

        [NativeTypeName("const char *(*)(const VSMap *)")]
        public delegate* unmanaged[Cdecl]<VSMap*, sbyte*> getError;

        [NativeTypeName("void (*)(const char *, VSFrameContext *)")]
        public delegate* unmanaged[Cdecl]<sbyte*, VSFrameContext*, void> setFilterError;

        [NativeTypeName("VSMap *(*)(VSPlugin *, const char *, const VSMap *)")]
        public delegate* unmanaged[Cdecl]<VSPlugin*, sbyte*, VSMap*, VSMap*> invoke;

        [NativeTypeName("const VSFormat *(*)(int, VSCore *)")]
        public delegate* unmanaged[Cdecl]<int, VSCore*, VSFormat*> getFormatPreset;

        [NativeTypeName("const VSFormat *(*)(int, int, int, int, int, VSCore *)")]
        public delegate* unmanaged[Cdecl]<int, int, int, int, int, VSCore*, VSFormat*> registerFormat;

        [NativeTypeName("const VSFrameRef *(*)(int, VSNodeRef *, char *, int)")]
        public delegate* unmanaged[Cdecl]<int, VSNode*, sbyte*, int, VSFrame*> getFrame;

        [NativeTypeName("void (*)(int, VSNodeRef *, VSFrameDoneCallback, void *)")]
        public delegate* unmanaged[Cdecl]<int, VSNode*, delegate* unmanaged[Cdecl]<void*, VSFrame*, int, VSNode*, sbyte*, void>, void*, void> getFrameAsync;

        [NativeTypeName("const VSFrameRef *(*)(int, VSNodeRef *, VSFrameContext *)")]
        public delegate* unmanaged[Cdecl]<int, VSNode*, VSFrameContext*, VSFrame*> getFrameFilter;

        [NativeTypeName("void (*)(int, VSNodeRef *, VSFrameContext *)")]
        public delegate* unmanaged[Cdecl]<int, VSNode*, VSFrameContext*, void> requestFrameFilter;

        [NativeTypeName("void (*)(VSNodeRef **, int *, VSFrameContext *)")]
        public delegate* unmanaged[Cdecl]<VSNode**, int*, VSFrameContext*, void> queryCompletedFrame;

        [NativeTypeName("void (*)(VSNodeRef *, int, VSFrameContext *)")]
        public delegate* unmanaged[Cdecl]<VSNode*, int, VSFrameContext*, void> releaseFrameEarly;

        [NativeTypeName("int (*)(const VSFrameRef *, int)")]
        public delegate* unmanaged[Cdecl]<VSFrame*, int, int> getStride;

        [NativeTypeName("const uint8_t *(*)(const VSFrameRef *, int)")]
        public delegate* unmanaged[Cdecl]<VSFrame*, int, byte*> getReadPtr;

        [NativeTypeName("uint8_t *(*)(VSFrameRef *, int)")]
        public delegate* unmanaged[Cdecl]<VSFrame*, int, byte*> getWritePtr;

        [NativeTypeName("VSFuncRef *(*)(VSPublicFunction, void *, VSFreeFuncData, VSCore *, const VSAPI *)")]
        public delegate* unmanaged[Cdecl]<delegate* unmanaged[Cdecl]<VSMap*, VSMap*, void*, VSCore*, VSAPI*, void>, void*, delegate* unmanaged[Cdecl]<void*, void>, VSCore*, VSAPI*, VSFunction*> createFunc;

        [NativeTypeName("void (*)(VSFuncRef *, const VSMap *, VSMap *, VSCore *, const VSAPI *)")]
        public delegate* unmanaged[Cdecl]<VSFunction*, VSMap*, VSMap*, VSCore*, VSAPI*, void> callFunc;

        [NativeTypeName("VSMap *(*)(void)")]
        public delegate* unmanaged[Cdecl]<VSMap*> createMap;

        [NativeTypeName("void (*)(VSMap *)")]
        public delegate* unmanaged[Cdecl]<VSMap*, void> freeMap;

        [NativeTypeName("void (*)(VSMap *)")]
        public delegate* unmanaged[Cdecl]<VSMap*, void> clearMap;

        [NativeTypeName("const VSVideoInfo *(*)(VSNodeRef *)")]
        public delegate* unmanaged[Cdecl]<VSNode*, VSVideoInfo3*> getVideoInfo;

        [NativeTypeName("void (*)(const VSVideoInfo *, int, VSNode3 *)")]
        public delegate* unmanaged[Cdecl]<VSVideoInfo3*, int, VSNode3*, void> setVideoInfo;

        [NativeTypeName("const VSFormat *(*)(const VSFrameRef *)")]
        public delegate* unmanaged[Cdecl]<VSFrame*, VSFormat*> getFrameFormat;

        [NativeTypeName("int (*)(const VSFrameRef *, int)")]
        public delegate* unmanaged[Cdecl]<VSFrame*, int, int> getFrameWidth;

        [NativeTypeName("int (*)(const VSFrameRef *, int)")]
        public delegate* unmanaged[Cdecl]<VSFrame*, int, int> getFrameHeight;

        [NativeTypeName("const VSMap *(*)(const VSFrameRef *)")]
        public delegate* unmanaged[Cdecl]<VSFrame*, VSMap*> getFramePropsRO;

        [NativeTypeName("VSMap *(*)(VSFrameRef *)")]
        public delegate* unmanaged[Cdecl]<VSFrame*, VSMap*> getFramePropsRW;

        [NativeTypeName("int (*)(const VSMap *)")]
        public delegate* unmanaged[Cdecl]<VSMap*, int> propNumKeys;

        [NativeTypeName("const char *(*)(const VSMap *, int)")]
        public delegate* unmanaged[Cdecl]<VSMap*, int, sbyte*> propGetKey;

        [NativeTypeName("int (*)(const VSMap *, const char *)")]
        public delegate* unmanaged[Cdecl]<VSMap*, sbyte*, int> propNumElements;

        [NativeTypeName("char (*)(const VSMap *, const char *)")]
        public delegate* unmanaged[Cdecl]<VSMap*, sbyte*, VSPropTypes> propGetType;

        [NativeTypeName("int64_t (*)(const VSMap *, const char *, int, int *)")]
        public delegate* unmanaged[Cdecl]<VSMap*, sbyte*, int, int*, long> propGetInt;

        [NativeTypeName("double (*)(const VSMap *, const char *, int, int *)")]
        public delegate* unmanaged[Cdecl]<VSMap*, sbyte*, int, int*, double> propGetFloat;

        [NativeTypeName("const char *(*)(const VSMap *, const char *, int, int *)")]
        public delegate* unmanaged[Cdecl]<VSMap*, sbyte*, int, int*, sbyte*> propGetData;

        [NativeTypeName("int (*)(const VSMap *, const char *, int, int *)")]
        public delegate* unmanaged[Cdecl]<VSMap*, sbyte*, int, int*, int> propGetDataSize;

        [NativeTypeName("VSNodeRef *(*)(const VSMap *, const char *, int, int *)")]
        public delegate* unmanaged[Cdecl]<VSMap*, sbyte*, int, int*, VSNode*> propGetNode;

        [NativeTypeName("const VSFrameRef *(*)(const VSMap *, const char *, int, int *)")]
        public delegate* unmanaged[Cdecl]<VSMap*, sbyte*, int, int*, VSFrame*> propGetFrame;

        [NativeTypeName("VSFuncRef *(*)(const VSMap *, const char *, int, int *)")]
        public delegate* unmanaged[Cdecl]<VSMap*, sbyte*, int, int*, VSFunction*> propGetFunc;

        [NativeTypeName("int (*)(VSMap *, const char *)")]
        public delegate* unmanaged[Cdecl]<VSMap*, sbyte*, int> propDeleteKey;

        [NativeTypeName("int (*)(VSMap *, const char *, int64_t, int)")]
        public delegate* unmanaged[Cdecl]<VSMap*, sbyte*, long, int, int> propSetInt;

        [NativeTypeName("int (*)(VSMap *, const char *, double, int)")]
        public delegate* unmanaged[Cdecl]<VSMap*, sbyte*, double, int, int> propSetFloat;

        [NativeTypeName("int (*)(VSMap *, const char *, const char *, int, int)")]
        public delegate* unmanaged[Cdecl]<VSMap*, sbyte*, sbyte*, int, int, int> propSetData;

        [NativeTypeName("int (*)(VSMap *, const char *, VSNodeRef *, int)")]
        public delegate* unmanaged[Cdecl]<VSMap*, sbyte*, VSNode*, int, int> propSetNode;

        [NativeTypeName("int (*)(VSMap *, const char *, const VSFrameRef *, int)")]
        public delegate* unmanaged[Cdecl]<VSMap*, sbyte*, VSFrame*, int, int> propSetFrame;

        [NativeTypeName("int (*)(VSMap *, const char *, VSFuncRef *, int)")]
        public delegate* unmanaged[Cdecl]<VSMap*, sbyte*, VSFunction*, int, int> propSetFunc;

        [NativeTypeName("int64_t (*)(int64_t, VSCore *)")]
        public delegate* unmanaged[Cdecl]<long, VSCore*, long> setMaxCacheSize;

        [NativeTypeName("int (*)(VSFrameContext *)")]
        public delegate* unmanaged[Cdecl]<VSFrameContext*, int> getOutputIndex;

        [NativeTypeName("VSFrameRef *(*)(const VSFormat *, int, int, const VSFrameRef **, const int *, const VSFrameRef *, VSCore *)")]
        public delegate* unmanaged[Cdecl]<VSFormat*, int, int, VSFrame**, int*, VSFrame*, VSCore*, VSFrame*> newVideoFrame2;

        [NativeTypeName("void (*)(VSMessageHandler, void *)")]
        public delegate* unmanaged[Cdecl]<delegate* unmanaged[Cdecl]<int, sbyte*, void*, void>, void*, void> setMessageHandler;

        [NativeTypeName("int (*)(int, VSCore *)")]
        public delegate* unmanaged[Cdecl]<int, VSCore*, int> setThreadCount;

        [NativeTypeName("const char *(*)(const VSPlugin *)")]
        public delegate* unmanaged[Cdecl]<VSPlugin*, sbyte*> getPluginPath;

        [NativeTypeName("const int64_t *(*)(const VSMap *, const char *, int *)")]
        public delegate* unmanaged[Cdecl]<VSMap*, sbyte*, int*, long*> propGetIntArray;

        [NativeTypeName("const double *(*)(const VSMap *, const char *, int *)")]
        public delegate* unmanaged[Cdecl]<VSMap*, sbyte*, int*, double*> propGetFloatArray;

        [NativeTypeName("int (*)(VSMap *, const char *, const int64_t *, int)")]
        public delegate* unmanaged[Cdecl]<VSMap*, sbyte*, long*, int, int> propSetIntArray;

        [NativeTypeName("int (*)(VSMap *, const char *, const double *, int)")]
        public delegate* unmanaged[Cdecl]<VSMap*, sbyte*, double*, int, int> propSetFloatArray;

        [NativeTypeName("void (*)(int, const char *)")]
        public delegate* unmanaged[Cdecl]<int, sbyte*, void> logMessage;

        [NativeTypeName("int (*)(VSMessageHandler, VSMessageHandlerFree, void *)")]
        public delegate* unmanaged[Cdecl]<delegate* unmanaged[Cdecl]<int, sbyte*, void*, void>, delegate* unmanaged[Cdecl]<void*, void>, void*, int> addMessageHandler;

        [NativeTypeName("int (*)(int)")]
        public delegate* unmanaged[Cdecl]<int, int> removeMessageHandler;

        [NativeTypeName("void (*)(VSCore *, VSCoreInfo *)")]
        public delegate* unmanaged[Cdecl]<VSCore*, VSCoreInfo*, void> getCoreInfo2;
    }
}
