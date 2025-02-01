using Mobsub.Native.VapoursynthBinding.Native.API;

namespace Mobsub.Native.VapoursynthBinding;

public unsafe class VsFrame : IDisposable
{
    internal readonly Mobsub.Native.VapoursynthBinding.Vapoursynth Api;
    public VSFrame* Handle;
    private bool disposed = false;
    public int FrameNumber { get; private set; }
    
    public VsFrame(Mobsub.Native.VapoursynthBinding.Vapoursynth vsapi, VSFrame* vsFrame, int frameNumber)
    {
        Api = vsapi;
        Handle = vsFrame;
        FrameNumber = frameNumber;
    }
    
    public VsFrame(Mobsub.Native.VapoursynthBinding.Vapoursynth vsapi, VSNode* vnode, int frameNumber)
    {
        Api = vsapi;
        Handle = vsapi.GetFramePtr(vnode, frameNumber);
        FrameNumber = frameNumber;
    }
    
    public void Dispose()
    {
        if (!disposed)
        {
            Api.FreeFramePtr(Handle);
            disposed = true;
        }
    }

    public nint GetStride(int plane) => Api.ApiVersion switch
    {
        3 => Api.Api3->getStride(Handle, plane),
        4 => Api.Api4->getStride(Handle, plane),
        _ => throw new ArgumentException()
    };

    public byte* GetReadPointer(int plane) => Api.ApiVersion switch
    {
        3 => Api.Api3->getReadPtr(Handle, plane),
        4 => Api.Api4->getReadPtr(Handle, plane),
        _ => throw new ArgumentException()
    };
}