using Mobsub.Native.VapoursynthBinding.Native.API;

namespace Mobsub.Native.VapoursynthBinding.Plugins;

public unsafe class Lsmas(VsCore core) : IVsPlugin
{
    private VSPlugin* plugin = core.GetPluginByNamespace("lsmas");
    public VsMap Invoke(string funcName, VsMap args) => IVsPlugin.Invoke(core, plugin, funcName, args);
    
    public VideoNode LWLibavSource(string source, string? cachefile = null)
    {
        var args = new VsMap(core.Api);
        args.SetData("source", source);
        args.SetData("cachefile", cachefile);
        var res = Invoke("LWLibavSource", args);
        var _clip = res.GetNode("clip", 0);
        res.Dispose();
        return new VideoNode(core, _clip);
    }

    public Dictionary<string, string[]> Version()
    {
        var map = new VsMap(core.Api);
        var res = Invoke("Version", map);

        var keyCount = res.KeysCount();
        Dictionary<string, string[]> values = [];
        for (var i = 0; i < keyCount; i++)
        {
            var key = res.GetKey(i);
            var valueCount = res.ElementsCount(key);
            var kvalues = new string[valueCount];

            for (var j = 0; j < valueCount; j++)
            {
                kvalues[j] = res.GetString(key, j);
            }

            values.Add(ConvertNative.StringFromPtr(key)!, kvalues);
        }

        return values;
    }
}