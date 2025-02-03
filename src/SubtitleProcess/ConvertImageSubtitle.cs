using System.Diagnostics;
using System.Text;
using Mobsub.Helper;
using Mobsub.SubtitleParse.PGS;
using SnippingToolOcrCore;

namespace Mobsub.SubtitleProcess;

public static class ConvertImageSubtitle
{
    public static void OcrPgsSup(string sub, string outputFile, byte imageBinarizeThreshold)
    {
        var ocrEngine = new Ocr();
        ocrEngine.CreatePipelineAndProcessOptions();
        var sb = new StringBuilder();
        
        foreach (var pic in PGSData.DecodeBitmapData(sub, imageBinarizeThreshold))
        {
            if (pic == null) continue;
            var newPic = ProcessImage(pic);
            
            unsafe
            {
                fixed (byte* p = newPic.GetPixelData())
                {
                    var ptr = (IntPtr)p;
                
                    var img = new Img()
                    {
                        t = 3,
                        col = newPic.GetWidth(),
                        row = newPic.GetHeight(),
                        _unk = 0,
                        step = newPic.GetStride(),
                        data_ptr = ptr
                    };
                
                    var result = ocrEngine.RunOcr(img);
                
                    if (result is null || result.Length == 0) continue;
                    if (result.Length > 1)
                    {
                        var i = 0;
                        foreach (var line in result)
                        {
                            // 30 ruby, 60 text?
                            if (line.Y3 - line.Y1 >= 30)
                            {
                                sb.Append(line.Text);

                                if (i < result.Length - 1)
                                {
                                    sb.Append("\\N");
                                }
                            }

                            i++;
                        }
                        
                        if (sb.Length > 0 && sb[^1] == 'N' && sb[^2] == '\\')
                        {
                            sb.Remove(sb.Length - 2, 2);
                        }
                        
                        sb.AppendLine();
                    }
                    else
                    {
                        sb.AppendLine(result[0].Text);
                    }
                }
            }
        }
        
        ocrEngine.Dispose();

        File.WriteAllText(outputFile, sb.ToString(), Encoding.UTF8);
    }

    private static SimpleBitmap ProcessImage(SimpleBitmap pic)
    {
        if (pic.GetWidth() >= 50 && pic.GetHeight() >= 50) return pic;

        var scale = pic.GetWidth() >= 25 && pic.GetHeight() >= 25 ? 2 : 4;
        return pic.ResizeNearest(scale);
    }
}