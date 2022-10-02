using ConfigFormat;
using Mobsub.AssFormat;
using Mobsub.Utils;
using YamlDotNet.Serialization.NamingConventions;
using YamlDotNet.Serialization;
using Mobsub.SubtitleProcess;

namespace Mobsub.SubtitleProcess2;

class AssProcess2
{
    /// <summary>
    /// Shift and Merge Ass from YAML config file.
    /// </summary>
    /// <param name="path"></param>
    /// <param name="output"></param>
    /// <param name="confFile"></param>
    /// <param name="confVar"> Ep, Language </param>
    /// <exception cref="Exception"></exception>
    internal static void Merge2(DirectoryInfo path, DirectoryInfo? output, FileInfo confFile, string[] confVar)
    {
        output ??= new DirectoryInfo(Path.Combine(path.FullName, "output"));
        _ = Files.CheckDir(output);
        var templatedShiftMergeYamlValue = new ShiftMergeYamlValue(confVar[0], confVar[1]);
        var shiftMergeAssDataList = new List<Dictionary<string, AssData>> { };

        if (!ConfigYaml.Extension.Contains(Path.GetExtension(confFile.Name)))
        {
            throw new Exception("ShiftMerge: Config file must be YAML.");
        }
        else
        {
            string ymlString = File.ReadAllText(confFile.FullName);
            ShiftMergeYamlV2 ymlDataV2;
            if (!ymlString.StartsWith("version", StringComparison.OrdinalIgnoreCase))
            {
                var ymlData = new DeserializerBuilder()
                    .WithNodeDeserializer(templatedShiftMergeYamlValue).WithNamingConvention(UnderscoredNamingConvention.Instance)
                    .Build().Deserialize<ShiftMergeYaml>(ymlString);
                 ymlDataV2 = ShiftMergeYaml.CovertToV2(ymlData);
            }
            else
            {
                ymlDataV2 = new DeserializerBuilder()
                .WithNodeDeserializer(templatedShiftMergeYamlValue).WithNamingConvention(UnderscoredNamingConvention.Instance)
                .Build().Deserialize<ShiftMergeYamlV2>(ymlString);
            }

            var mergeDict = ymlDataV2.Namef;
            var fps = ymlDataV2.Fps;
            var tplDict = ymlDataV2.TplFr;
            var shiftEpDict = ymlDataV2.ShiftFr[confVar[0]];

            var masterAss = mergeDict["master"];
            var toAssFile = new FileInfo(Path.Combine(path.FullName, $"{masterAss}.ass"));
            var optAssFile = new FileInfo(Path.Combine(output.FullName, $"{masterAss}.ass"));
            var toAssData = AssParse.Parse(Files.Read(toAssFile));
            var toAssDataArr = new Dictionary<string, AssData>[] { toAssData };

            foreach ( var key in shiftEpDict.Keys )
            {
                var fromAssFile = new FileInfo(Path.Combine(path.FullName, $"{mergeDict[key]}.ass"));
                var fromAssData = AssParse.Parse(Files.Read(fromAssFile));
                var shiftAssData = new Dictionary<string, AssData>(fromAssData);
                var shiftEpCode = shiftEpDict[key];

                if (shiftEpCode.Length > 1 && (shiftEpCode[1] - tplDict[key] != shiftEpCode[0]))
                {
                    throw new Exception("ShiftMerge: Please check your shift_fr, it may be wrong.");
                }
                else
                {
                    var spanTime = TimecodesConvert.ConvertToSpan($"{shiftEpDict[key][0]}frm", fps);
                    shiftAssData["Events"].Table = AssProcess.Shift(fromAssData["Events"].Table, spanTime);
                }
                shiftMergeAssDataList.Add(shiftAssData);
            }

            var newAssData = AssParse.JoinSections(AssProcess.Merge(toAssDataArr, shiftMergeAssDataList.ToArray(), "all")[0]);

            Files.Write(optAssFile, newAssData);
        }
    }
}