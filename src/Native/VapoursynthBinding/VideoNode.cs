using System.Diagnostics;
using System.Runtime.InteropServices;
using Mobsub.Native.VapoursynthBinding.Native.API;

namespace Mobsub.Native.VapoursynthBinding;

public unsafe class VideoNode : IDisposable
{
    public VideoNode(VsCore core, VSNode* node)
    {
        Core = core;
        Handle = node;
    }
    
    public readonly VsCore Core;
    public VSNode* Handle;
    private bool disposed = false;

    public void Dispose()
    {
        if (!disposed)
        {
            switch (Core.Api.ApiVersion)
            {
                case 3:
                    Core.Api.Api3->freeNode(Handle);
                    break;
                case 4:
                    Core.Api.Api4->freeNode(Handle);
                    break;
            }
            disposed = true;
        }
    }
    
    public VSVideoInfo GetVideoInfo()
    {
        switch (Core.Api.ApiVersion)
        {
            case 3:
                var infoPtr3 = Core.Api.Api3->getVideoInfo(Handle);
                var format3 = infoPtr3->format;
                var vFormat3 = new VSVideoFormat
                {
                    colorFamily = format3->colorFamily,
                    sampleType = format3->sampleType,
                    bitsPerSample = format3->bitsPerSample,
                    bytesPerSample = format3->bytesPerSample,
                    subSamplingW = format3->subSamplingW,
                    subSamplingH = format3->subSamplingH,
                    numPlanes = format3->numPlanes,
                };
                //formatId = (uint)format3->id;
                return new VSVideoInfo
                {
                    format = vFormat3,
                    fpsNum = infoPtr3->fpsNum,
                    fpsDen = infoPtr3->fpsDen,
                    width = infoPtr3->width,
                    height = infoPtr3->height,
                    numFrames = infoPtr3->numFrames
                };
            case 4:
                var infoPtr = Core.Api.Api4->getVideoInfo(Handle);
                var vFormat = infoPtr->format;
                //formatId = core.Vs.Api4->queryVideoFormatID((int)vFormat.colorFamily, (int)vFormat.sampleType, vFormat.bitsPerSample, vFormat.subSamplingW, vFormat.subSamplingH, core.core);
                return new VSVideoInfo
                {
                    format = vFormat,
                    fpsNum = infoPtr->fpsNum,
                    fpsDen = infoPtr->fpsDen,
                    width = infoPtr->width,
                    height = infoPtr->height,
                    numFrames = infoPtr->numFrames,
                };

            default:
                throw new Exception();
        }
    }
    
    public bool TryGetFrame(int frameNumber, out VsFrame? frame)
    {
        if (frameNumber < 0)
        {
            frame = null;
            return false;
        }
        
        var ptr = Core.Api.GetFramePtr(Handle, frameNumber);
        if ((IntPtr)ptr == IntPtr.Zero)
        {
            frame = null;
            return false;
        }

        frame = new VsFrame(Core.Api, ptr, frameNumber);
        return true;
    }
    
    
    public Task<VsFrame> GetFrameAsync(int frameNumber)
    {
        Debug.WriteLine($"Requesting frame {frameNumber}...");
        var tcs = new TaskCompletionSource<VsFrame>();
        VSFrameDoneCallback frameDoneCallback = FrameDoneCallback;
        var frameDoneCallbackPtr = (delegate* unmanaged[Cdecl]<void*, VSFrame*, int, VSNode*, sbyte*, void>)Marshal.GetFunctionPointerForDelegate(frameDoneCallback);
        var handle = GCHandle.Alloc(tcs, GCHandleType.Normal);

        switch (Core.Api.ApiVersion)
        {
            case 3:
                Core.Api.Api3->getFrameAsync(frameNumber, Handle, frameDoneCallbackPtr, (void*)GCHandle.ToIntPtr(handle));
                break;
            case 4:
                Core.Api.Api4->getFrameAsync(frameNumber, Handle, frameDoneCallbackPtr, (void*)GCHandle.ToIntPtr(handle));
                break;
        }
        
        return tcs.Task;
    }
    
    private void FrameDoneCallback(void* userData, VSFrame* vsFrame, int n, VSNode* vsNode, sbyte* error)
    {
        var handle = GCHandle.FromIntPtr((IntPtr)userData);
        var tcs = (TaskCompletionSource<VsFrame>)handle.Target!;
        handle.Free();
        
        if (error != null)
        {
            var errorMessage = Marshal.PtrToStringAnsi((IntPtr)error);
            Console.WriteLine($"Error retrieving frame: {errorMessage}");
        }
        
        tcs.TrySetResult(new VsFrame(Core.Api, vsFrame, n));
        Debug.WriteLine($"Frame {n} completed.");
    }
    
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private unsafe delegate void VSFrameDoneCallback(void* userData, VSFrame* vsFrame, int n, VSNode* vsNode, sbyte* error);
}