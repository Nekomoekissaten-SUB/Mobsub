using Mobsub.Native.VapoursynthBinding.Native.API;

namespace Mobsub.Native.VapoursynthBinding.Plugins;

public unsafe class Fmtc(VsCore core) : IVsPlugin
{
    private VSPlugin* plugin = core.GetPluginByNamespace("fmtc");
    public VsMap Invoke(string funcName, VsMap args) => IVsPlugin.Invoke(core, plugin, funcName, args);

    public VideoNode Resample(VideoNode clip,
        long? w = null, long? h = null, double[]? sx = null, double[]? sy = null, double[]? sw = null, double[]? sh = null,
        string[]? kernel = null, long[]? taps = null, double[]? a1 = null, double[]? a2 = null,
        long? csp = null, string? css = null, double[]? planes = null, bool? fulls = null, bool? fulld = null,
        string? cplace = null, bool? flt = null)
    {
        var args = new VsMap(core.Api);
        args.SetNode("clip", clip.Handle);
        args.SetInt("w", w);
        args.SetInt("h", h);
        args.SetFloatArray("sx", sx);
        args.SetFloatArray("sy", sy);
        args.SetFloatArray("sw", sw);
        args.SetFloatArray("sh", sh);
        args.SetDataArray("kernel", kernel);
        args.SetIntArray("taps", taps);
        args.SetFloatArray("a1", a1);
        args.SetFloatArray("a2", a2);
        args.SetInt("csp", csp);
        args.SetData("css", css);
        args.SetFloatArray("planes", planes);
        args.SetBool("fulls", fulls);
        args.SetBool("fulld", fulld);
        args.SetData("cplace", cplace);
        args.SetBool("flt", flt);
        var res = Invoke("resample", args);

        var _clip = res.GetNode("clip", 0);
        res.Dispose();
        return new VideoNode(core, _clip);
    }
    public VideoNode Bitdepth(VideoNode clip, long? csp = null, long? bits = null, long? flt = null, long[]? planes = null, bool? fulls = null, bool? fulld = null, long? dmode = null, double? ampo = null, double? ampn = null)
    {
        var args = new VsMap(core.Api);
        args.SetNode("clip", clip.Handle);
        args.SetInt("csp", csp);
        args.SetInt("bits", bits);
        args.SetInt("flt", flt);
        args.SetIntArray("planes", planes);
        args.SetBool("fulls", fulls);
        args.SetBool("fulld", fulld);
        args.SetInt("dmode", dmode);
        args.SetFloat("ampo", ampo);
        args.SetFloat("ampn", ampn);
        var res = Invoke("bitdepth", args);

        var _clip = res.GetNode("clip", 0);
        res.Dispose();
        return new VideoNode(core, _clip);
    }
    public VideoNode Matrix(VideoNode clip, string? mat = null, string? mats = null, string? matd = null, bool? fulls = null, bool? fulld = null, double[]? coef = null, long? csp = null, long? col_fam = null, long? bits = null)
    {
        var args = new VsMap(core.Api);
        args.SetNode("clip", clip.Handle);
        args.SetData("mat", mat);
        args.SetData("mats", mats);
        args.SetData("matd", matd);
        args.SetBool("fulls", fulls);
        args.SetBool("fulld", fulld);
        args.SetFloatArray("coef", coef);
        args.SetInt("csp", csp);
        args.SetInt("col_fam", col_fam);
        args.SetInt("bits", bits);
        var res = Invoke("matrix", args);

        var _clip = res.GetNode("clip", 0);
        res.Dispose();
        return new VideoNode(core, _clip);
    }
    public VideoNode matrix2020cl(VideoNode clip, bool? full = null, long? csp = null, long? bits = null)
    {
        var args = new VsMap(core.Api);
        args.SetNode("clip", clip.Handle);
        args.SetBool("full", full);
        args.SetInt("csp", csp);
        args.SetInt("bits", bits);
        var res = Invoke("matrix2020cl", args);

        var _clip = res.GetNode("clip", 0);
        res.Dispose();
        return new VideoNode(core, _clip);
    }
}