using Mobsub.SubtitleParse.AssTypes;
using System.Text;

namespace Mobsub.SubtitleProcess;

public class Utils
{
    public static void RemoveChar(StringBuilder sb, char[] chars)
    {
        for (int i = 0; i < sb.Length; i++)
        {
            foreach (var c in chars)
            {
                if (sb[i] == c)
                {
                    sb.Remove(i, 1);
                    i--;
                }
            }
        }
    }

    public static void RemoveChar(StringBuilder sb, char c)
    {
        for (int i = 0; i < sb.Length; i++)
        {
            if (sb[i] == c)
            {
                sb.Remove(i, 1);
                i--;
            }
        }
    }

    public static FileInfo[] Traversal(DirectoryInfo path, string ext) => path.GetFiles().Where(fi => fi.Extension == ext).ToArray();

    public static TimeSpan GetTimespan(string shiftSpan, string fps)
    {
        var ss = shiftSpan.AsSpan();
        TimeSpan tsp;

        if (ss.Length > 3 && ss[^4] >= '0' && ss[^4] <= '9')
        {
            int v = 0;
            var negative = false;
            for (var i = 0; i < ss.Length - 3; i++)
            {
                var c = ss[i];
                if (c == '-')
                {
                    if (i == 0)
                    {
                        negative = true;
                    }
                    else
                    {
                        throw new ArgumentException("Invalid --by value");
                    }

                }
                else
                {
                    v = c - '0' + (v * 10);
                }
            }
            v = negative ? 0 - v : v;

            tsp = ss[^3..] switch
            {
                "mls" => TimeSpan.FromMicroseconds(v),
                "cts" => TimeSpan.FromMicroseconds(v * 10),
                "sec" => TimeSpan.FromSeconds(v),
                "min" => TimeSpan.FromMinutes(v),
                "frm" => TimeSpan.FromMilliseconds(FrameToMillisecond(v, UnifiedFps(fps))),
                _ => throw new ArgumentException("Invalid suffixes"),
            };
        }
        else
        {
            tsp = TimeSpan.FromSeconds(Convert.ToInt32(shiftSpan));
        }
        return tsp;
    }

    public static double FrameToMillisecond(int frame, decimal fps) => (double)(frame / fps) * 1000;

    public static decimal UnifiedFps(string fpsString)
    {
        string[] Arr24 = ["23.976", "23.98"];
        string[] Arr30 = ["29.970", "29.97"];
        string[] Arr60 = ["59.940", "59.94"];

        if (fpsString.Contains('/'))
        {
            var fpsA = fpsString.Split('/').Select(s => Convert.ToDecimal(s.Trim())).ToArray();
            return fpsA[0] / fpsA[1];
        }
        else if (Arr24.Contains(fpsString))
        {
            return 24000 / 1001M;
        }
        else if (Arr30.Contains(fpsString))
        {
            return 30000 / 1001M;
        }
        else if (Arr60.Contains(fpsString))
        {
            return 60000 / 1001M;
        }
        else
        {
            return Convert.ToDecimal(fpsString);
        }
    }

    public static void WriteAssToPath(AssData data, FileInfo baseFile, FileSystemInfo optPath)
    {
        switch (optPath)
        {
            case FileInfo f:
                data.WriteAssFile(f.FullName);
                break;
            case DirectoryInfo dir:
                if (!dir.Exists)
                {
                    dir.Create();
                }
                data.WriteAssFile(Path.Combine(dir.FullName, baseFile.Name));
                break;
        }
    }

    public static FileInfo ChangeSuffix(FileInfo f, string suffix) => ChangeSuffix(f, f.Directory!, suffix);
    public static FileInfo ChangeSuffix(FileInfo f, DirectoryInfo dir, string suffix)
    {
        return new FileInfo(Path.Combine(dir.FullName, Path.GetFileNameWithoutExtension(f.FullName) + suffix));
    }

}
