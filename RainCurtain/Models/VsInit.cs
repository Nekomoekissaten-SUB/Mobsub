using Mobsub.Native.VapoursynthBinding;
using Mobsub.Native.VapoursynthBinding.Scripts;

namespace Mobsub.RainCurtain.Models;

public class VsInit
{
    public VsInit(int apiVersion)
    {
        VsApi = new Vapoursynth(apiVersion);
        VsCore = new VsCore(VsApi);
        VsScript = new Scripts(VsCore);
    }
    
    internal readonly Vapoursynth VsApi;
    internal readonly VsCore VsCore;
    internal readonly Scripts VsScript;
}