using Mobsub.Ikkoku.SubtileProcess;
using Mobsub.SubtitleParse.AssTypes;
using Mobsub.ZhConvert;
using OpenCCSharp.Conversion;
using System.Text;

namespace Mobsub.Ikkoku.CommandLine;

internal class CJKppCmd
{
    internal static void Execute(FileSystemInfo path, FileSystemInfo opt, FileInfo config)
    {
        var dicts = OpenCCSharpUtils.LoadJson(config);
        var converter = OpenCCSharpUtils.GetConverter(dicts);

        switch (path)
        {
            case FileInfo f:
                switch (opt)
                {
                    case FileInfo fo:
                        ConvertAssByOpencc(f, fo, converter);
                        break;
                    case DirectoryInfo diro:
                        if (!diro.Exists)
                        {
                            diro.Create();
                        }
                        ConvertAssByOpencc(f, new FileInfo(Path.Combine(diro.FullName, f.Name)), converter);
                        break;
                }
                break;

            case DirectoryInfo dir:

                var subfiles = Utils.Traversal(dir, ".ass");
                switch (opt)
                {
                    case DirectoryInfo diro:

                        if (!diro.Exists)
                        {
                            diro.Create();
                        }
                        foreach (var f in subfiles)
                        {
                            ConvertAssByOpencc(f, new FileInfo(Path.Combine(diro.FullName, f.Name)), converter);
                        }

                        break;
                }
                break;
        }
    }

    internal static async Task BuildOpenccsharpDict(FileSystemInfo path, FileSystemInfo opt)
    {
        var targetSuffix = ".tris";

        switch (path)
        {
            case FileInfo f:
                switch (opt)
                {
                    case FileInfo fo:
                        await OpenCCSharpUtils.BuildTriesDictionary(f, fo);
                        break;
                    case DirectoryInfo diro:
                        if (!diro.Exists)
                        {
                            diro.Create();
                        }
                        await OpenCCSharpUtils.BuildTriesDictionary(f, Utils.ChangeSuffix(f, targetSuffix));
                        break;
                }
                break;

            case DirectoryInfo dir:

                var subfiles = Utils.Traversal(dir, ".txt");
                switch (opt)
                {
                    case DirectoryInfo diro:

                        if (!diro.Exists)
                        {
                            diro.Create();
                        }
                        foreach (var f in subfiles)
                        {
                            await OpenCCSharpUtils.BuildTriesDictionary(f, Utils.ChangeSuffix(f, diro, targetSuffix));
                        }

                        break;
                }
                break;
        }
    }

    private static void ConvertAssByOpencc(FileInfo f, FileInfo opt, ChainedScriptConverter converter)
    {
        if (f.FullName.Equals(opt.FullName))
        {
            throw new ArgumentException("Output file path can’t same as input file!");
        }

        Console.WriteLine($"Input: {f}");
        Console.WriteLine($"Output: {opt}");
        var data = new AssData();
        data.ReadAssFile(f.FullName);
        var sb = new StringBuilder();
        Dictionary<int, string[]> changesRecord = [];

        foreach (var et in data.Events.Collection)
        {
            if (!CJKpp.NotZhConvert(et))
            {
                CJKpp.ZhConvertEventLineByOpenccsharp(et.Text, sb, converter, out string[]? countChanges);

                if (countChanges is not null)
                {
                    changesRecord.Add(et.lineNumber, countChanges);
                }
            }
        }

        data.WriteAssFile(opt.FullName);

        if (changesRecord.Keys.Count > 0)
        {
            Console.WriteLine("Please pay attention:");
            foreach (var pair in changesRecord)
            {
                Console.WriteLine($"Linenumber: {pair.Key}");
                Console.WriteLine(pair.Value[0]);
                Console.WriteLine(pair.Value[1]);
                Console.WriteLine();
            }
        }

        Console.WriteLine("fine");
        Console.WriteLine();
    }
}
