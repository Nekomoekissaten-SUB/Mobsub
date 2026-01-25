using Mobsub.SubtitleProcess;
using Mobsub.SubtitleParse.AssTypes;
using Mobsub.Helper.ZhConvert;
using OpenCCSharp.Conversion;
using System.CommandLine;
using System.Text;
using System.Text.Json;

namespace Mobsub.Ikkoku.CommandLine;

internal enum CJKppMode
{
    OpenCC,
    Fanhuaji
}


internal class CJKppCmd
{
    internal static Command Build(Argument<FileSystemInfo> path, Option<FileSystemInfo> optPath, Option<FileInfo> convConf)
    {
        var modeOpt = new Option<CJKppMode>("--mode")
        {
            Description = "Conversion mode.",
            DefaultValueFactory = _ => CJKppMode.OpenCC
        };

        var cjkppCommand = new Command("cjkpp", "CJK post-processor, such as simplified and traditional conversion of Hanzi.")
        {
            path, optPath, convConf, modeOpt
        };
        cjkppCommand.Validators.Add((result) =>
        {
            switch (result.GetValue(optPath))
            {
                case FileInfo:
                    switch (result.GetValue(path))
                    {
                        case DirectoryInfo:
                            result.AddError("Output path must be directory when input path is a dir!");
                            break;
                    }
                    break;
            }
        }
        );

        cjkppCommand.SetAction(async result =>
        {
            var pathValue = result.GetValue(path);
            var optPathValue = result.GetValue(optPath);
            var configFile = result.GetValue(convConf);
            var mode = result.GetValue(modeOpt);

            if (configFile is null && mode == CJKppMode.OpenCC)
            {
                throw new ArgumentException("Please specify a conversion config file for OpenCC mode!");
            }
            await ExecuteAsync(pathValue!, optPathValue!, configFile, mode);
        });

        // subcommand build-dict
        cjkppCommand.Add(BuildSubDict(path, optPath));

        return cjkppCommand;
    }

    private static Command BuildSubDict(Argument<FileSystemInfo> path, Option<FileSystemInfo> optPath)
    {
        var convDictCommand = new Command("build-dict", "Build tris dictinaries from txt files (txt file same as OpenCC).")
        {
            path, optPath
        };

        convDictCommand.Validators.Add((result) =>
        {
            switch (result.GetValue(optPath))
            {
                case FileInfo:
                    switch (result.GetValue(path))
                    {
                        case DirectoryInfo:
                            result.AddError("Output path must be directory when input path is a dir!");
                            break;
                    }
                    break;
            }
        }
        );

        convDictCommand.SetAction(async result =>
        {
            var pathValue = result.GetValue(path);
            var optPathValue = result.GetValue(optPath);
            await BuildOpenccsharpDict(pathValue!, optPathValue!);
        });

        return convDictCommand;
    }

    internal static async Task ExecuteAsync(FileSystemInfo path, FileSystemInfo opt, FileInfo? config, CJKppMode mode)
    {
        if (mode == CJKppMode.OpenCC)
        {
            var dicts = OpenCCSharpUtils.LoadJson(config!);
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
        else if (mode == CJKppMode.Fanhuaji)
        {
            FanhuajiOptions options;
            if (config != null && config.Exists)
            {
                using var fs = config.OpenRead();
                options = JsonSerializer.Deserialize(fs, FanhuajiJsonContext.Default.FanhuajiOptions) ?? new FanhuajiOptions();
            }
            else
            {
                options = new FanhuajiOptions();
            }

            using var httpClient = new HttpClient();
            var client = new FanhuajiClient(httpClient);
            var converter = new ConvertFanhuaji(options);

            switch (path)
            {
                case FileInfo f:
                    switch (opt)
                    {
                        case FileInfo fo:
                            await ConvertAssByFanhuaji(f, fo, converter, client);
                            break;
                        case DirectoryInfo diro:
                            if (!diro.Exists)
                            {
                                diro.Create();
                            }
                            await ConvertAssByFanhuaji(f, new FileInfo(Path.Combine(diro.FullName, f.Name)), converter, client);
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
                                await ConvertAssByFanhuaji(f, new FileInfo(Path.Combine(diro.FullName, f.Name)), converter, client);
                            }

                            break;
                    }
                    break;
            }
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
        var events = data.Events ?? throw new InvalidDataException($"ASS events missing in {f.FullName}.");
        var evtConverter = new ConvertSimplifiedChinese(converter);
        Dictionary<int, string[]> changesRecord = [];

        for (var i = 0; i < events.Collection.Count; i++)
        {
            var evt = events.Collection[i];
            evtConverter.ZhConvertEventByOpenccSharp(ref evt, changesRecord);
            events.Collection[i] = evt;
        }

        data.WriteAssFile(opt.FullName);

        if (changesRecord.Keys.Count > 0)
        {
            Console.WriteLine("Please pay attention:");
            foreach (var pair in changesRecord)
            {
                Console.WriteLine($"LineNumber: {pair.Key}");
                Console.WriteLine(pair.Value[0]);
                Console.WriteLine(pair.Value[1]);
                Console.WriteLine();
            }
        }

        Console.WriteLine("fine");
        Console.WriteLine();
    }

    private static async Task ConvertAssByFanhuaji(FileInfo f, FileInfo opt, ConvertFanhuaji converter, FanhuajiClient client)
    {
        if (f.FullName.Equals(opt.FullName))
        {
            throw new ArgumentException("Output file path can’t same as input file!");
        }

        Console.WriteLine($"Input: {f}");
        Console.WriteLine($"Output: {opt}");
        var data = new AssData();
        data.ReadAssFile(f.FullName);
        var events = data.Events ?? throw new InvalidDataException($"ASS events missing in {f.FullName}.");

        try
        {
            await converter.ConvertEventsAsync(events.Collection, client);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error during conversion: {ex.GetType().Name}: {ex.Message}");
            Console.WriteLine(ex.StackTrace);
            throw;
        }

        data.WriteAssFile(opt.FullName);

        Console.WriteLine("fine");
        Console.WriteLine();
    }
}
