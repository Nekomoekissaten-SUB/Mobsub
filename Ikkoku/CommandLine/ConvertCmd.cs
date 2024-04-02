using Mobsub.Ikkoku.SubtileProcess;
using Mobsub.SubtitleParse.AssTypes;

namespace Mobsub.Ikkoku.CommandLine;

internal class ConvertCmd
{
    internal static void Execute(FileSystemInfo path, FileSystemInfo? optPath, string convertSuffix, string inputSuffix)
    {
        switch (path)
        {
            case FileInfo f:
                ConvertSubtitle(f, optPath, convertSuffix);
                break;
            case DirectoryInfo d:
                var files = Utils.Traversal(d, inputSuffix);
                foreach (var f in files)
                {
                    ConvertSubtitle(f, optPath, convertSuffix);
                }
                break;
        }
    }

    internal static void ConvertSubtitle(FileInfo fromFile, FileSystemInfo? optPath, string convertSuffix)
    {
        if (fromFile.Extension == convertSuffix)
        {
            throw new Exception($"{convertSuffix} can’t same as {fromFile.Extension}");
        }

        DirectoryInfo optDir = fromFile.Directory!;
        switch (optPath)
        {
            case DirectoryInfo d:
                optDir = d;
                break;
            case FileInfo f:
                optDir = f.Directory!;
                break;
            default:
                break;
        }

        var optFile = Utils.ChangeSuffix(fromFile, optDir, convertSuffix);
        var fs = new FileStream(optFile.FullName, FileMode.Create, FileAccess.Write);
        using var memStream = new MemoryStream();
        using var sw = new StreamWriter(memStream, SubtitleParse.Utils.EncodingRefOS());

        switch (fromFile.Extension)
        {
            case ".ass":
                var ass = new AssData();
                ass.ReadAssFile(fromFile.FullName);

                switch (convertSuffix)
                {
                    case ".txt":
                        ConvertSub.ConvertAssToTxt(sw, ass);
                        break;
                    default:
                        // fs.Close();
                        throw new NotImplementedException($"Unsupport: {fromFile.Extension} convert to {convertSuffix}.");
                }

                break;
            default:
                // fs.Close();
                throw new NotImplementedException($"Unsupport: {fromFile.Extension}.");
        }

        sw.Flush();

        memStream.Seek(0, SeekOrigin.Begin);
        memStream.CopyTo(fs);
        // fs.Close();
    }
}
