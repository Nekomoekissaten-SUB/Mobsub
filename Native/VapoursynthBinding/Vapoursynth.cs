using Mobsub.Native.VapoursynthBinding.Native.API;
using API4 = Mobsub.Native.VapoursynthBinding.Native.API;
using API3 = Mobsub.Native.VapoursynthBinding.Native.API.API3;

namespace Mobsub.Native.VapoursynthBinding;

using API_VSAPI = API4.VSAPI;
using VSAPI = API3.VSAPI;

public unsafe class Vapoursynth(int version) : IDisposable
{
    private IntPtr _api = (IntPtr)Methods.getVapourSynthAPI(version);
    private bool _disposed = false;
    
    public readonly int ApiVersion = version;
    public VSAPI* Api3 => (VSAPI*)_api;
    public API_VSAPI* Api4 => (API_VSAPI*)_api;

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                // Dispose managed resources.
            }
            _api = IntPtr.Zero;
            _disposed = true;
        }
    }

    internal VSCore* CreateCorePtr(int threads = 0, VSCoreCreationFlags flags = 0) => ApiVersion switch
    {
        3 => Api3->createCore(threads),
        4 => Api4->createCore((int)flags),
        _ => throw new ArgumentException()
    };
    internal void FreeCorePtr(VSCore* core)
    {
        switch (ApiVersion)
        {
            case 3:
                Api3->freeCore(core);
                break;
            case 4:
                Api4->freeCore(core);
                break;
        }
    }
    internal VSMap* CreateMapPtr() => ApiVersion switch
    {
        3 => Api3->createMap(),
        4 => Api4->createMap(),
        _ => throw new ArgumentException()
    };
    internal void FreeMapPtr(VSMap* map)
    {
        switch (ApiVersion)
        {
            case 3:
                Api3->freeMap(map);
                break;
            case 4:
                Api4->freeMap(map);
                break;
        }
    }

    public VsCore CreateCore() => new VsCore(this, CreateCorePtr());
    
    public void FreeCore(VsCore vsCore) => vsCore.Dispose();
    
    internal VSFrame* GetFramePtr(VSNode* vnode, int frameNumber) => ApiVersion switch
    {
        3 => Api3->getFrame(frameNumber, vnode, (sbyte*)IntPtr.Zero, 0),
        4 => Api4->getFrame(frameNumber, vnode, (sbyte*)IntPtr.Zero, 0),
        _ => throw new ArgumentException()
    };
    internal void FreeFramePtr(VSFrame* frame)
    {
        switch (ApiVersion)
        {
            case 3:
                Api3->freeFrame(frame);
                break;
            case 4:
                Api4->freeFrame(frame);
                break;
        }
    }
}