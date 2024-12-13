using Mobsub.Native.VapoursynthBinding;
using Mobsub.Native.VapoursynthBinding.Plugins;

namespace Mobsub.RainCurtain.Models.Video;

public class VsLwLibav(VsInit vs)
{
    internal Lsmas Lsmas = new Lsmas(vs.VsCore);

    internal VideoNode OpenVideo(string videoName)
    {
        var src = Lsmas.LWLibavSource(videoName);
        // sourceInfo = src.GetVideoInfo();
        return vs.VsScript.ToRGB(src);
    }

    internal VsVideoInfo GetVideoInfo(string videoName)
    {
        var src = Lsmas.LWLibavSource(videoName);
        return src.GetVideoInfo();
    }
}