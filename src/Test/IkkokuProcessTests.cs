using Mobsub.Helper.ZhConvert;
using Mobsub.SubtitleProcess;
using Mobsub.SubtitleProcess.FormatData;
using Mobsub.SubtitleParse.AssTypes;

namespace Mobsub.Test;

[TestClass]
public class IkkokuProcessTests
{
    private static readonly string TestFilesRoot = Path.Combine(AppContext.BaseDirectory, "test_files");
    private static readonly string DictRoot = Path.Combine(TestFilesRoot, "dicts");

    private static string TestFile(string name) => Path.Combine(TestFilesRoot, name);
    private static string DictFile(string name) => Path.Combine(DictRoot, name);

    private static string CreateTempDir()
    {
        var root = Path.Combine(Path.GetTempPath(), "mobsub_tests");
        var dir = Path.Combine(root, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static void CleanupDir(string dir)
    {
        if (Directory.Exists(dir))
        {
            Directory.Delete(dir, true);
        }
    }

    private static void AssertFileEqual(string expectedPath, string actualPath)
    {
        var expected = File.ReadAllBytes(expectedPath);
        var actual = File.ReadAllBytes(actualPath);
        CollectionAssert.AreEqual(expected, actual, $"Mismatch: {Path.GetFileName(expectedPath)}");
    }

    private static void AssertTextEqual(string expected, string actualPath)
    {
        var actual = File.ReadAllText(actualPath);
        Assert.AreEqual(expected, actual, $"Mismatch: {Path.GetFileName(actualPath)}");
    }

    private static string JoinLines(string newline, params string[] lines)
        => string.Join(newline, lines) + newline;

    [TestMethod]
    public void CleanMore_Ass()
    {
        var input = TestFile("clean_more.ass");
        var expected = JoinLines("\n",
            "[Script Info]",
            "Title: clean_more",
            "ScriptType: v4.00+",
            "WrapStyle: 0",
            "PlayResX: 1280",
            "PlayResY: 720",
            "LayoutResX: 1280",
            "LayoutResY: 720",
            "",
            "[V4+ Styles]",
            "Format: Name, Fontname, Fontsize, PrimaryColour, SecondaryColour, OutlineColour, BackColour, Bold, Italic, Underline, StrikeOut, ScaleX, ScaleY, Spacing, Angle, BorderStyle, Outline, Shadow, Alignment, MarginL, MarginR, MarginV, Encoding",
            "Style: Default,Arial,40,&H00FFFFFF,&H000000FF,&H00000000,&H64000000,0,0,0,0,100,100,0,0,1,2,2,2,10,10,10,1",
            "Style: Unused,Arial,30,&H00FFFFFF,&H000000FF,&H00000000,&H00000000,0,0,0,0,100,100,0,0,1,2,2,2,10,10,10,1",
            "",
            "[Events]",
            "Format: Layer, Start, End, Style, Name, MarginL, MarginR, MarginV, Effect, Text",
            "Comment: 0,0:00:02.00,0:00:01.00,Default,,0,0,0,,Bad time",
            "Dialogue: 0,0:00:03.00,0:00:04.00,Default,,0,0,0,,Trim me",
            "Comment: 0,0:00:04.00,0:00:05.00,Default,,0,0,0,,Comment line");
        var tempDir = CreateTempDir();
        var output = Path.Combine(tempDir, "clean_more.ass");

        try
        {
            var data = new AssData();
            data.ReadAssFileAsync(input).GetAwaiter().GetResult();

            var args = new Clean.CleanAssArgs
            {
                keepComment = false,
                renameTitle = true,
                addLayoutRes = true,
                dropUnusedStyles = false,
                processEvents = true,
                rmMotionGarbage = true,
                deleteFanhuaji = true,
                dropDuplicateStyles = true,
                fixStyleName = true
            };

            Clean.CleanAss(data, Path.GetFileNameWithoutExtension(input).AsSpan(), args, out _, out var untouched);
            if (!untouched)
            {
                data.WriteAssFile(output, forceEnv: false, ctsRounding: false);
            }
            else
            {
                File.Copy(input, output, true);
            }

            AssertTextEqual(expected, output);
        }
        finally
        {
            CleanupDir(tempDir);
        }
    }

    [TestMethod]
    public void Convert_AssToTxt()
    {
        var input = TestFile("convert_tags.ass");
        var expected = JoinLines("\r\n",
            @"Hello World\\NNext line",
            "Curly  drop");
        var tempDir = CreateTempDir();
        var output = Path.Combine(tempDir, "convert_tags.txt");

        try
        {
            var data = new AssData();
            data.ReadAssFile(input);

            using (var fs = new FileStream(output, FileMode.Create, FileAccess.Write))
            using (var memStream = new MemoryStream())
            using (var sw = new StreamWriter(memStream, Mobsub.SubtitleParse.Utils.EncodingRefOS()))
            {
                ConvertSub.ConvertAssToTxt(sw, data);
                sw.Flush();
                memStream.Position = 0;
                memStream.CopyTo(fs);
            }

            AssertTextEqual(expected, output);
        }
        finally
        {
            CleanupDir(tempDir);
        }
    }

    [TestMethod]
    public void Tpp_ShiftStyles()
    {
        var input = TestFile("tpp_shift.ass");
        var expected = JoinLines("\n",
            "[Script Info]",
            "Title: TppShift",
            "ScriptType: v4.00+",
            "PlayResX: 640",
            "PlayResY: 360",
            "",
            "[V4+ Styles]",
            "Format: Name, Fontname, Fontsize, PrimaryColour, SecondaryColour, OutlineColour, BackColour, Bold, Italic, Underline, StrikeOut, ScaleX, ScaleY, Spacing, Angle, BorderStyle, Outline, Shadow, Alignment, MarginL, MarginR, MarginV, Encoding",
            "Style: Default,Arial,30,&H00FFFFFF,&H000000FF,&H00000000,&H00000000,0,0,0,0,100,100,0,0,1,1,1,2,10,10,10,1",
            "Style: JP,Arial,30,&H00FFFFFF,&H000000FF,&H00000000,&H00000000,0,0,0,0,100,100,0,0,1,1,1,2,10,10,10,1",
            "Style: CH,Arial,30,&H00FFFFFF,&H000000FF,&H00000000,&H00000000,0,0,0,0,100,100,0,0,1,1,1,2,10,10,10,1",
            "",
            "[Events]",
            "Format: Layer, Start, End, Style, Name, MarginL, MarginR, MarginV, Effect, Text",
            "Dialogue: 0,0:00:01.00,0:00:02.00,JP,,0,0,0,,JP line",
            "Dialogue: 0,0:00:02.00,0:00:03.00,CH,,0,0,0,,CH line");
        var tempDir = CreateTempDir();
        var output = Path.Combine(tempDir, "tpp_shift.ass");

        try
        {
            var data = new AssData();
            data.ReadAssFile(input);
            var events = data.Events ?? throw new InvalidDataException($"ASS events missing in {input}.");

            Tpp.ShiftAss(events.Collection, TimeSpan.FromMilliseconds(500), ["CH"], negation: false);
            data.WriteAssFile(output);

            AssertTextEqual(expected, output);
        }
        finally
        {
            CleanupDir(tempDir);
        }
    }

    [TestMethod]
    public void Tpp_AssumeFps_Vfr()
    {
        var input = TestFile("tpp_vfr.ass");
        var expected = JoinLines("\n",
            "[Script Info]",
            "Title: TppVfr",
            "ScriptType: v4.00+",
            "PlayResX: 640",
            "PlayResY: 360",
            "",
            "[V4+ Styles]",
            "Format: Name, Fontname, Fontsize, PrimaryColour, SecondaryColour, OutlineColour, BackColour, Bold, Italic, Underline, StrikeOut, ScaleX, ScaleY, Spacing, Angle, BorderStyle, Outline, Shadow, Alignment, MarginL, MarginR, MarginV, Encoding",
            "Style: Default,Arial,30,&H00FFFFFF,&H000000FF,&H00000000,&H00000000,0,0,0,0,100,100,0,0,1,1,1,2,10,10,10,1",
            "",
            "[Events]",
            "Format: Layer, Start, End, Style, Name, MarginL, MarginR, MarginV, Effect, Text",
            "Dialogue: 0,0:00:00.04,0:00:00.08,Default,,0,0,0,,Frame 1",
            "Dialogue: 0,0:00:00.12,0:00:00.16,Default,,0,0,0,,Frame 2");
        var tempDir = CreateTempDir();
        var output = Path.Combine(tempDir, "tpp_vfr.ass");

        try
        {
            var data = new AssData();
            data.ReadAssFile(input);
            var events = data.Events ?? throw new InvalidDataException($"ASS events missing in {input}.");

            var tcfile = TestFile("tcfile_v2.txt");
            using var sr = new StreamReader(tcfile);
            var tcdata = new AVTimestamp();
            var version = AVTimestamp.CheckVersion(sr);
            tcdata.Parse(sr, version);

            Tpp.AssumeFPS(events.Collection, tcdata, "25/1");
            data.WriteAssFile(output);

            AssertTextEqual(expected, output);
        }
        finally
        {
            CleanupDir(tempDir);
        }
    }

    [TestMethod]
    public void Merge_BaseAdd_All()
    {
        var baseFile = TestFile("merge_base.ass");
        var mergeFile = TestFile("merge_add.ass");
        var expected = JoinLines("\n",
            "[Script Info]",
            "Title: MergeBase",
            "ScriptType: v4.00+",
            "PlayResX: 640",
            "PlayResY: 360",
            "",
            "[V4+ Styles]",
            "Format: Name, Fontname, Fontsize, PrimaryColour, SecondaryColour, OutlineColour, BackColour, Bold, Italic, Underline, StrikeOut, ScaleX, ScaleY, Spacing, Angle, BorderStyle, Outline, Shadow, Alignment, MarginL, MarginR, MarginV, Encoding",
            "Style: Default,Arial,30,&H00FFFFFF,&H000000FF,&H00000000,&H00000000,0,0,0,0,100,100,0,0,1,1,1,2,10,10,10,1",
            "Style: Shared,Arial,20,&H00FFFFFF,&H000000FF,&H00000000,&H00000000,0,0,0,0,100,100,0,0,1,1,1,2,10,10,10,1",
            "Style: NewStyle,Arial,36,&H00FFFFFF,&H000000FF,&H00000000,&H00000000,0,0,0,0,100,100,0,0,1,1,1,2,10,10,10,1",
            "",
            "[Events]",
            "Format: Layer, Start, End, Style, Name, MarginL, MarginR, MarginV, Effect, Text",
            "Dialogue: 0,0:00:01.00,0:00:02.00,Shared,,0,0,0,,Base line",
            "Dialogue: 0,0:00:02.00,0:00:03.00,Shared,,0,0,0,,Merged shared line",
            "Dialogue: 0,0:00:03.00,0:00:04.00,NewStyle,,0,0,0,,New style line");
        var tempDir = CreateTempDir();
        var output = Path.Combine(tempDir, "merge_base.ass");

        try
        {
            var baseData = new AssData();
            baseData.ReadAssFile(baseFile);
            var mergeData = new AssData();
            mergeData.ReadAssFile(mergeFile);

            MergeCommon.MergeAss(baseData, [mergeData], "all");
            baseData.WriteAssFile(output);

            AssertTextEqual(expected, output);
        }
        finally
        {
            CleanupDir(tempDir);
        }
    }

    [TestMethod]
    public void Merge_ByConfig()
    {
        var config = TestFile("merge_config.yml");
        var expected = JoinLines("\n",
            "[Script Info]",
            "Title: CfgMaster",
            "ScriptType: v4.00+",
            "PlayResX: 640",
            "PlayResY: 360",
            "",
            "[V4+ Styles]",
            "Format: Name, Fontname, Fontsize, PrimaryColour, SecondaryColour, OutlineColour, BackColour, Bold, Italic, Underline, StrikeOut, ScaleX, ScaleY, Spacing, Angle, BorderStyle, Outline, Shadow, Alignment, MarginL, MarginR, MarginV, Encoding",
            "Style: Default,Arial,30,&H00FFFFFF,&H000000FF,&H00000000,&H00000000,0,0,0,0,100,100,0,0,1,1,1,2,10,10,10,1",
            "Style: sub-1-CH,Arial,30,&H00FFFFFF,&H000000FF,&H00000000,&H00000000,0,0,0,0,100,100,0,0,1,1,1,2,10,10,10,1",
            "",
            "[Events]",
            "Format: Layer, Start, End, Style, Name, MarginL, MarginR, MarginV, Effect, Text",
            "Dialogue: 0,0:00:01.00,0:00:02.00,Default,,0,0,0,,Master line",
            "Comment: 0,0:00:02.00,0:00:03.00,sub-1-CH,,0,0,0,,Should be commented",
            "Dialogue: 0,0:00:03.00,0:00:04.00,Default,,0,0,0,,Sub line");
        var outputDir = CreateTempDir();

        try
        {
            var merger = new MergeByConfig(config);
            var output = merger.Merge("01", "jp", TestFilesRoot, outputDir, "all");

            AssertTextEqual(expected, output);
        }
        finally
        {
            CleanupDir(outputDir);
        }
    }

    [TestMethod]
    public async Task Cjkpp_BuildDict()
    {
        var input = TestFile("cjkpp_dict.txt");
        var expected = DictFile("cjkpp_dict.tris");
        var tempDir = CreateTempDir();
        var output = Path.Combine(tempDir, "cjkpp_dict.tris");

        try
        {
            await OpenCCSharpUtils.BuildTriesDictionary(new FileInfo(input), new FileInfo(output));

            AssertFileEqual(expected, output);
        }
        finally
        {
            CleanupDir(tempDir);
        }
    }

    [TestMethod]
    public void Cjkpp_Convert()
    {
        var input = TestFile("cjkpp.ass");
        var expected = JoinLines("\n",
            "[Script Info]",
            "Title: Cjkpp",
            "ScriptType: v4.00+",
            "PlayResX: 640",
            "PlayResY: 360",
            "",
            "[V4+ Styles]",
            "Format: Name, Fontname, Fontsize, PrimaryColour, SecondaryColour, OutlineColour, BackColour, Bold, Italic, Underline, StrikeOut, ScaleX, ScaleY, Spacing, Angle, BorderStyle, Outline, Shadow, Alignment, MarginL, MarginR, MarginV, Encoding",
            "Style: Default,Arial,30,&H00FFFFFF,&H000000FF,&H00000000,&H00000000,0,0,0,0,100,100,0,0,1,1,1,2,10,10,10,1",
            "Style: JP,Arial,30,&H00FFFFFF,&H000000FF,&H00000000,&H00000000,0,0,0,0,100,100,0,0,1,1,1,2,10,10,10,1",
            "",
            "[Events]",
            "Format: Layer, Start, End, Style, Name, MarginL, MarginR, MarginV, Effect, Text",
            "Dialogue: 0,0:00:01.00,0:00:02.00,Default,,0,0,0,,汉龙",
            "Dialogue: 0,0:00:02.00,0:00:03.00,JP,,0,0,0,,漢龍");
        var tempDir = CreateTempDir();
        var output = Path.Combine(tempDir, "cjkpp.ass");

        try
        {
            var config = new FileInfo(TestFile("cjkpp_config.json"));
            var dicts = OpenCCSharpUtils.LoadJson(config);
            var converter = OpenCCSharpUtils.GetConverter(dicts);

            var data = new AssData();
            data.ReadAssFile(input);
            var events = data.Events ?? throw new InvalidDataException($"ASS events missing in {input}.");

            var evtConverter = new ConvertSimplifiedChinese(converter);
            for (var i = 0; i < events.Collection.Count; i++)
            {
                var evt = events.Collection[i];
                evtConverter.ZhConvertEventByOpenccSharp(ref evt);
                events.Collection[i] = evt;
            }

            data.WriteAssFile(output);

            AssertTextEqual(expected, output);
        }
        finally
        {
            CleanupDir(tempDir);
        }
    }
}
