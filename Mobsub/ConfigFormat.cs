using YamlDotNet.RepresentationModel;
using YamlDotNet.Serialization;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;

namespace ConfigFormat;

internal class ShiftMergeYaml
{
    public string PythonBin { get; set; }
    public List<string> Namef { get; set; }
    public string Fps { get; set; }
    public Dictionary<string, string[]> ShiftFr { get; set; }

    internal static ShiftMergeYamlV2 CovertToV2(ShiftMergeYaml yamlData)
    {
        var yamlDataV2 = new ShiftMergeYamlV2();
        var v2SubKeyList = new List<string>();

        for (var i = 0; i < yamlData.Namef.Count; i++)
        {
            string v2SubKey;
            if (i == 0)
            {
                v2SubKey = "master";
            }
            else
            {
                v2SubKey = $"sub-{i}";
            }
            v2SubKeyList.Add(v2SubKey);
            yamlDataV2.Namef[v2SubKey] = yamlData.Namef[i];
        }

        yamlDataV2.Fps = yamlData.Fps;
        yamlDataV2.TplFr = new Dictionary<string, int> { };

        foreach (var k in yamlData.ShiftFr.Keys)
        {
            var frameArr = yamlData.ShiftFr[k];
            var valueDict = new Dictionary<string, int[]> { };
            for (var i = 0; i < frameArr.Length; i++)
            {
                if (frameArr[i] != "x")
                {
                    valueDict.Add(v2SubKeyList[i + 1], new int[1] { int.Parse(frameArr[i]) });
                }
            }
            if (valueDict.Count > 0)
                yamlDataV2.ShiftFr.Add(k, valueDict);
        }
        return yamlDataV2;
    }
}

internal class ShiftMergeYamlV2
{
    public int Version { get; set; } = 2;
    public Dictionary<string, string> Namef { get; set; } = new Dictionary<string, string>();
    public string Fps { get; set; } = "24000/1001";
    public Dictionary<string, int> TplFr { get; set; } = new Dictionary<string, int>();
    public Dictionary<string, Dictionary<string, int[]>> ShiftFr { get; set; } = new Dictionary<string, Dictionary<string, int[]>>();
}

/// https://github.com/aaubry/YamlDotNet/issues/321
internal class ShiftMergeYamlValue : INodeDeserializer
{
    private string ep;
    private string lang;

    public ShiftMergeYamlValue(string ep, string lang)
    {
        this.ep = ep;
        this.lang = lang;
    }

    bool INodeDeserializer.Deserialize(IParser parser, Type expectedType, Func<IParser, Type, object> nestedObjectDeserializer, out object value)
    {
        if (expectedType != typeof(string))
        {
            value = null;
            return false;
        }

        var scalar = parser.TryConsume<Scalar>(out var template);

        if (scalar == null)
        {
            value = null;
            return false;
        }

        value = template.Value.Replace("{{ep}}", ep).Replace("{{lang}}", lang);
        return true;
    }
}