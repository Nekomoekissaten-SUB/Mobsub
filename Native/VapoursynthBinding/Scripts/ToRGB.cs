using Mobsub.Native.VapoursynthBinding.Native.API;
using Mobsub.Native.VapoursynthBinding.Plugins;

namespace Mobsub.Native.VapoursynthBinding.Scripts;

public partial class Scripts(VsCore core)
{
    private readonly int _apiVersion = core.Api.ApiVersion;
    
    public VideoNode ToRGB(VideoNode clip, string? matrix = null, int? depth = null, VSSampleType? sample = null, bool? full = null, string? kernel = null, int? taps = null, float? a1 = null, float? a2 = null, string? cplace = null)
    {
        var _matrix = GetMatrixString(clip, matrix, true);
        var sFormat = clip.GetVideoInfo().format;

        var sColorFamily = sFormat.colorFamily;
        var sIsRGB = sColorFamily is VSColorFamily.cmRGB or VSColorFamily.cfRGB;
        var sIsYUV = sColorFamily is VSColorFamily.cmYUV or VSColorFamily.cfYUV;
        var sIsGRAY = sColorFamily is VSColorFamily.cmGray or VSColorFamily.cfGray;

        var sbitPS = sFormat.bitsPerSample;
        var sSType = sFormat.sampleType;

        var sHSubS = 1 << sFormat.subSamplingW;
        var sVSubS = 1 << sFormat.subSamplingH;

        var fulls = full ?? (((sIsGRAY || sIsYUV) && matrix == "RGB" || matrix == "YCgCo" || matrix == "OPP") ? true :
            (sIsYUV || sIsGRAY) ? false : true);

        // Get properties of output clip
        var dbitPS = depth ?? ((sSType != VSSampleType.stFloat && sample == VSSampleType.stFloat) ? 32 :
            (sSType != VSSampleType.stInteger && sample == VSSampleType.stInteger) ? 16 : sbitPS);
        var dSType = sample ?? (depth is null ? sSType : dbitPS >= 32 ? VSSampleType.stFloat : VSSampleType.stInteger);
        if (dSType == VSSampleType.stInteger && (dbitPS < 1 || dbitPS > 16))
        {
            throw new Exception($"{dbitPS}-bit integer output is not supported!");
        }
        if (dSType == VSSampleType.stFloat && (dbitPS != 16 || dbitPS != 32))
        {
            throw new Exception($"{dbitPS}-bit float output is not supported!");
        }

        var fulld = true;

        // Get properties of internal processed clip
        // If float sample type is involved, then use float for conversion
        var pSType = (VSSampleType)Math.Max((byte)sSType, (byte)dSType);
        int pbitPS;
        if (pSType == VSSampleType.stFloat)
        {
            // For float sample type, only 32-bit is supported by fmtconv
            pbitPS = 32;
        }
        else
        {
            // Apply conversion in the higher one of input and output bit depth
            pbitPS = Math.Max(sbitPS, (int)dbitPS);
            // For integer sample type, only 8-, 9-, 10-, 12-, 16-bit is supported by fmtc.matrix
            if (sHSubS != 1 || sVSubS != 1)
            {
                // When chroma re-sampling is needed, always process in 16-bit for integer sample type
                pbitPS = 16;
            }
            else if (pbitPS == 11)
            {
                pbitPS = 12;
            }
            else if (pbitPS > 12 && pbitPS < 16)
            {
                pbitPS = 16;
            }
        }

        // fmtc.resample parameters
        if (kernel is null)
        {
            kernel = "bicubic";
            if (a1 is null && a2 is null)
            {
                a1 = 0;
                a2 = 0.5f;
            }
        }

        // Conversion
        var fmtc = new Fmtc(core);
        var std = new Std(core);
        long[] rgbMatrix = [0, 0, 0];

        if (sIsRGB)
        {
            return fmtc.Bitdepth(clip, bits: dbitPS);
        }
        else if (sIsGRAY)
        {
            var c = fmtc.Bitdepth(clip, bits: dbitPS);
            VideoNode[] clips = [c, c, c];
            long[] planes = [0, 0, 0];
            var cRGB = std.ShufflePlanes(clips, planes, _apiVersion == 3 ? (long)VSColorFamily.cmRGB : (long)VSColorFamily.cfRGB);
            var res = std.SetFrameProp(cRGB, "_Matrix", rgbMatrix);
            
            c.Dispose();
            cRGB.Dispose();
            return res;
        }
        else
        {
            VideoNode c444;
            if (sHSubS != 1 || sVSubS != 1)
            {
                var _kernel = new string[3] { kernel, kernel, kernel };
                var _taps = taps is null ? null : new long[3] { (long)taps!, (long)taps!, (long)taps! };
                var _a1 = a1 is null ? null : new double[3] { (double)a1!, (double)a1!, (double)a1! };
                var _a2 = a2 is null ? null : new double[3] { (double)a2!, (double)a2!, (double)a2! };
                var _planes = new double[3] { 2, 3, 3 };
                c444 = fmtc.Resample(clip, kernel: _kernel, taps: _taps, a1: _a1, a2: _a2, css: "444", planes: _planes, fulls: fulls, fulld: fulls, cplace: cplace, flt: pSType == VSSampleType.stFloat);
            }
            else
            {
                c444 = fmtc.Bitdepth(clip, bits: pbitPS, flt: (long)pSType, fulls: fulls, fulld: fulls);
            }

            VideoNode cCorrectMatrix;
            if (_matrix == "OPP")
            {
                var coef = new double[] { 1, 1, 2 / 3, 0, 1, 0, -4 / 3, 0, 1, -1, 2 / 3, 0 };
                var cConvertMatrix = fmtc.Matrix(c444, fulls: fulls, fulld: fulld, coef: coef, col_fam: (long)VSColorFamily.cfRGB);
                cCorrectMatrix = std.SetFrameProp(cConvertMatrix, "_Matrix", rgbMatrix);
                cConvertMatrix.Dispose();
            }
            else if (_matrix == "2020cl")
            {
                cCorrectMatrix = fmtc.matrix2020cl(c444, full: fulls);
            }
            else
            {
                cCorrectMatrix = fmtc.Matrix(c444, mat: _matrix, fulls: fulls, fulld: fulld, col_fam: 2);
            }

            var res = fmtc.Bitdepth(cCorrectMatrix, bits: dbitPS, flt: (long)dSType, fulls: fulld, fulld: fulld);

            c444.Dispose();
            cCorrectMatrix.Dispose();
            return res;
        }
    }

    public string GetMatrixString(VideoNode clip, string? matrix = null, bool? dIsRGB = null)
    {
        var videoInfo = clip.GetVideoInfo();
        var sFormat = videoInfo.format;
        var sColorFamily = sFormat.colorFamily;
        var sIsRGB = sColorFamily == VSColorFamily.cmRGB || sColorFamily == VSColorFamily.cfRGB;
        dIsRGB ??= !sIsRGB;

        // Resolution level
        var noneD = false;
        var SD = false;
        var HD = false;
        var UHD = false;

        if (videoInfo.width <= 0 || videoInfo.height <= 0)
        {
            noneD = true;
        }
        else if (videoInfo.width <= 1024 || videoInfo.height <= 576)
        {
            SD = true;
        }
        else if (videoInfo.width <= 2048 || videoInfo.height <= 1536)
        {
            HD = true;
        }
        else
        {
            UHD = true;
        }

        // Convert to string format
        return matrix switch
        {
            "rgb" => "RGB",
            "709" or "bt709" => "709",
            "unspecified" or "unspec" or null => ((bool)dIsRGB && sIsRGB) ? "RGB" : SD ? "601" : UHD ? "2020" : "709",
            "fcc" => "FCC",
            "bt470bg" or "470bg" => "601",  // bt470bg
            "601" or "smpte170m" or "170m" => "601",    // smpte170m
            "240" or "smpte240m" => "240",  // smpte240m
            "ycgco" or "ycocg" => "YCgCo",
            "2020" or "bt2020nc" or "2020ncl" => "2020",    // bt2020nc
            "2020cl" or "bt2020c" => "2020cl", // bt2020c
            "opp" or "opponent" => "OPP",    // opponent color space
            _ => throw new ArgumentException("Unsupported matrix specified!")
        };
    }
}