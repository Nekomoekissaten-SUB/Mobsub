using System.Diagnostics;
using System.Text;
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
            unsafe
            {
                fixed (byte* p = pic.GetPixelData())
                {
                    var ptr = (IntPtr)p;
                
                    var img = new Img()
                    {
                        t = 3,
                        col = pic.GetWidth(),
                        row = pic.GetHeight(),
                        _unk = 0,
                        step = pic.GetStride(),
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
}