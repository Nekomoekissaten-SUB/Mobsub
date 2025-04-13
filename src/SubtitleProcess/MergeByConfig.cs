using System.Diagnostics;
using System.Text;
using Mobsub.SubtitleParse.AssTypes;
using Mobsub.SubtitleProcess.FormatData;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Mobsub.SubtitleProcess;

public class MergeByConfig(string configPath)
{
    private ShiftMergeYamlV2 ConfigDataBase { get; set; } = ParseConfigDataBase(configPath);
    
    private static ShiftMergeYamlV2 ParseConfigDataBase(string configPath)
    {
        var ymlString = File.ReadAllText(configPath);
        if (ymlString.Length == 0)
        {
            throw new FormatException("Configuration file is empty.");
        }
        var deserializer = new StaticDeserializerBuilder(new YamlStaticContext()).WithNamingConvention(UnderscoredNamingConvention.Instance).Build();
        if (!ymlString.AsSpan().StartsWith("version")) { Debug.Assert(ShiftMergeYaml.ConvertToV2File(configPath)); }
        var ymlData = ymlString.AsSpan().StartsWith("version") ? deserializer.Deserialize<ShiftMergeYamlV2>(ymlString) : deserializer.Deserialize<ShiftMergeYaml>(ymlString).CovertToV2();
        Debug.Assert(ymlData is not null);
        return ymlData;
    }
    
    public string Merge(string episode, string lang, string baseDir, string optPath, string mergeSection)
    {
        var sb = new StringBuilder(ConfigDataBase.Namef["master"]).Replace("{{ep}}", episode).Replace("{{lang}}", lang);
        sb.Append(".ass");
        var baseFileName = sb.ToString();
        var mergeDataList = new List<AssData>();
    
        // string optFileName;
        // switch (optPath)
        // {
        //     case DirectoryInfo d:
        //         if (!d.Exists)
        //         {
        //             d.Create();
        //         }
        //         optFileName = Path.Combine(optPath.FullName, baseFileName);
        //         break;
        //     default:
        //         throw new ArgumentException("Output must be a directory.");
        // }
        
        if (!Directory.Exists(optPath)) { Directory.CreateDirectory(optPath); }

        string optFileName;
        if (ConfigDataBase.OutputName is not null)
        {
            sb.Clear();
            sb.Append(ConfigDataBase.OutputName.Replace("{{ep}}", episode).Replace("{{lang}}", lang));
            sb.Append(".ass");
            optFileName = Path.Combine(optPath, sb.ToString());
        }
        else
        {
            optFileName = Path.Combine(optPath, baseFileName);
        }
        
        var baseData = new AssData();
        var bf = Path.Combine(baseDir, baseFileName);
        if (!File.Exists(bf))
        {
            throw new FileNotFoundException($"File {bf} not found.");
        }
        baseData.ReadAssFile(bf);
        
        string[]? mergePartName = null;
        foreach (var kvp in ConfigDataBase.ShiftFr.Where(kvp => kvp.Key == episode))
        {
            TimeSpan tsp;
            foreach (var kvpEp in kvp.Value)
            {
                if (kvpEp.Value.Length > 3 || (kvpEp.Value.Length > 1 && kvpEp.Value[1] != -1 && (kvpEp.Value[1] - ConfigDataBase.TplFr[kvpEp.Key] != kvpEp.Value[0])))
                {
                    throw new Exception("Merge: Please check your configuration file shift_fr, it may be wrong.");
                }

                var mf = Path.Combine(baseDir, $"{ConfigDataBase.Namef[kvpEp.Key].Replace("{{ep}}", episode).Replace("{{lang}}", lang)}.ass");
                if (!File.Exists(mf))
                {
                    throw new FileNotFoundException($"File {mf} not found.");
                }
                
                var dataFrom = new AssData();
                dataFrom.ReadAssFile(mf);
                tsp = Utils.GetTimespan($"{kvpEp.Value[0]}frm", ConfigDataBase.Fps);
    
                if (kvpEp.Value.Length == 3 && kvpEp.Value[2] != -1)
                {
                    var start = new AssTime((int)Utils.FrameToMillisecond(kvpEp.Value[2], Utils.UnifiedFps(ConfigDataBase.Fps)));
                    Tpp.ShiftAss(dataFrom.Events.Collection, tsp, start);
                }
                else
                {
                    Tpp.ShiftAss(dataFrom.Events.Collection, tsp);
                }
    
                mergeDataList.Add(dataFrom);
            }
            if (!ConfigDataBase.ConvertFromV1) { mergePartName = kvp.Value.Keys.ToArray(); }
        }
    
        if (mergeDataList.Count == 0)
        {
            File.Copy(bf, optFileName);
        }
        else
        {
            MergeCommon.MergeAss(baseData, [.. mergeDataList], mergeSection, mergePartName);
            baseData.WriteAssFile(optFileName);
        }

        return optFileName;
    }
}