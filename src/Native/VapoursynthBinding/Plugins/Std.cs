using Mobsub.Native.VapoursynthBinding.Native.API;

namespace Mobsub.Native.VapoursynthBinding.Plugins;

public unsafe class Std(VsCore core) : IVsPlugin
{
    private VSPlugin* plugin = core.GetPluginByNamespace("std");
    public VsMap Invoke(string funcName, VsMap args) => IVsPlugin.Invoke(core, plugin, funcName, args);
    
    public VideoNode ShufflePlanes(VideoNode[] clips, long[] planes, long colorfamily)
    {
        var args = new VsMap(core.Api);
        foreach (var c in clips)
        {
            args.SetNode("clips", c.Handle);
        }
        args.SetIntArray("planes", planes);
        args.SetInt("colorfamily", colorfamily);
        var res = Invoke("ShufflePlanes", args);

        var _clip = res.GetNode("clip", 0);
        res.Dispose();
        return new VideoNode(core, _clip);
    }
    public VideoNode SetFrameProp(VideoNode clip, string prop, long[] intval)
    {
        var args = new VsMap(core.Api);
        args.SetNode("clip", clip.Handle);
        args.SetData("prop", prop);
        args.SetIntArray("intval", intval);
        var res = Invoke("SetFrameProp", args);

        var _clip = res.GetNode("clip", 0);
        res.Dispose();
        return new VideoNode(core, _clip);
    }
}