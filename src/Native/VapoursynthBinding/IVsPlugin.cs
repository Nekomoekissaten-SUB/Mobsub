using Mobsub.Native.VapoursynthBinding.Native.API;

namespace Mobsub.Native.VapoursynthBinding;

public unsafe interface IVsPlugin
{
    VsMap Invoke(string funcName, VsMap args);
    internal static VsMap Invoke(VsCore core, VSPlugin* plugin, string funcName, VsMap args)
    {
        var map = core.Api.ApiVersion switch
        {
            3 => core.Api.Api3->invoke(plugin, ConvertNative.StringToPtr(funcName), args.Map),
            4 => core.Api.Api4->invoke(plugin, ConvertNative.StringToPtr(funcName), args.Map),
            _ => throw new Exception()
        };
        args.Dispose();
        return new VsMap(core.Api, map);
    }
}