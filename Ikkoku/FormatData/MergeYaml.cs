using YamlDotNet.Serialization;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using System.Text;

namespace Mobsub.Ikkoku.FormatData;

[YamlSerializable]
public class ShiftMergeYaml
{
    public string? PythonBin;
    public string[] Namef = [];
    public string Fps = "24000/1001";
    public Dictionary<string, string[]> ShiftFr = [];

    public ShiftMergeYamlV2 CovertToV2()
    {
        var yamlDataV2 = new ShiftMergeYamlV2();
        var v2SubKeys = new string[Namef.Length];

        for (var i = 0; i < Namef.Length; i++)
        {
            v2SubKeys[i] = (i == 0) ? "master" : $"sub-{i}";
            yamlDataV2.Namef[v2SubKeys[i]] = Namef[i];
        }

        yamlDataV2.Fps = Fps;

        foreach (var k in ShiftFr.Keys)
        {
            var frameArr = ShiftFr[k];
            var valueDict = new Dictionary<string, int[]> { };
            for (var i = 0; i < frameArr.Length; i++)
            {
                if (frameArr[i] != "x")
                {
                    valueDict.Add(v2SubKeys[i + 1], [int.Parse(frameArr[i])]);
                }
            }
            if (valueDict.Count > 0)
            {
                yamlDataV2.ShiftFr.Add(k, valueDict);
            }
        }
        return yamlDataV2;
    }
}

[YamlSerializable]
public class ShiftMergeYamlV2
{
    public int Version = 2;
    public Dictionary<string, string> Namef = [];
    public string Fps = "24000/1001";
    public Dictionary<string, int> TplFr = [];
    public Dictionary<string, Dictionary<string, int[]>> ShiftFr = [];
}

[YamlStaticContext]
[YamlSerializable(typeof(ShiftMergeYaml))]
[YamlSerializable(typeof(ShiftMergeYamlV2))]
public partial class YamlStaticContext : StaticContext
{
}

/// https://github.com/aaubry/YamlDotNet/issues/321
internal class ShiftMergeYamlValue(string ep, string lang) : INodeDeserializer
{
    private readonly string ep = ep;
    private readonly string lang = lang;

    bool INodeDeserializer.Deserialize(IParser parser, Type expectedType, Func<IParser, Type, object?> nestedObjectDeserializer, out object? value)
    {
        if (expectedType != typeof(string))
        {
            value = null;
            return false;
        }

        _ = parser.TryConsume<Scalar>(out var template);

        if (template is null)
        {
            value = null;
            return false;
        }
        else
        {
            value = new StringBuilder(template.Value).Replace("{{ep}}", ep).Replace("{{lang}}", lang).ToString();
            return true;
        }
    
    }
}
