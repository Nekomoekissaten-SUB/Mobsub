using YamlDotNet.Serialization;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using System.Text;

namespace Mobsub.SubtitleProcess.FormatData;

[YamlSerializable]
public class ShiftMergeYaml
{
    public string? PythonBin;
    public string[] Namef = [];
    public string Fps = "24000/1001";
    public Dictionary<string, string[]> ShiftFr = [];

    public ShiftMergeYamlV2 CovertToV2()
    {
        var yamlDataV2 = new ShiftMergeYamlV2() { ConvertFromV1 = true };
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

    private enum SectionKey
    {
        PythonBin,
        Namef,
        Fps,
        ShiftFr,
    }
    public static bool ConvertToV2File(string v1file)
    {
        var text = File.ReadAllLines(v1file);
        if (text.Length == 0) { return false; }
        var v2file = Utils.ChangeSuffix(new FileInfo(v1file), ".v2.yml");
        var mismatch = false;
        using var sw = File.CreateText(v2file.FullName);

        sw.WriteLine("version: 2");
        sw.WriteLine();

        SectionKey key = SectionKey.PythonBin;
        var fileCount = 0;
        foreach (var line in text)
        {
            var span = line.AsSpan();
            if (span.EndsWith(":"))
            {
                if (span.StartsWith("python_bin"))
                {
                    key = SectionKey.PythonBin;
                }
                if (span.StartsWith("namef"))
                {
                    key = SectionKey.Namef;
                    sw.WriteLine("namef:");
                }
                if (span.StartsWith("fps"))
                {
                    key = SectionKey.Fps;
                    sw.WriteLine("fps:");
                }
                if (span.StartsWith("shift_fr"))
                {
                    key = SectionKey.ShiftFr;
                    sw.WriteLine("shift_fr:");
                }
                continue;
            }
            
            if (key == SectionKey.PythonBin) { continue; }
            if (key == SectionKey.Namef)
            {
                if (line != string.Empty && span.TrimStart().StartsWith("-"))
                {
                    var h1 = span.IndexOf('\'');
                    h1 = h1 == -1 ? span.IndexOf('"') : h1;
                    if (h1 == -1) { continue; }
                    var content = span[h1..];
                    switch (fileCount)
                    {
                        case 0:
                            sw.Write("  master: ");
                            break;
                        default:
                            sw.Write($"  sub-{fileCount}: ");
                            break;
                    }
                    sw.WriteLine(content);
                    fileCount++;
                }
            }
            if (key == SectionKey.Fps) { sw.WriteLine(span); }
            if (key == SectionKey.ShiftFr)
            {
                var sepIdx = span.IndexOf(':');
                var _value = span[(sepIdx + 1)..].Trim()[1..^1];
                sw.WriteLine(span[..(sepIdx + 1)]);

                var valSepIdx = -1;
                int nextValSepIdx;
                var shiftCount = 1;
                while ((nextValSepIdx = _value[(valSepIdx+1)..].IndexOf(',')) > -1)
                {
                    sw.Write($"    sub-{shiftCount}: [");
                    sw.Write(_value[(valSepIdx + 1)..nextValSepIdx].TrimStart());
                    sw.WriteLine(']');
                    valSepIdx = nextValSepIdx;
                    shiftCount++;
                }
                sw.Write($"    sub-{shiftCount}: [");
                sw.Write(_value[(valSepIdx + 1)..].TrimStart());
                sw.WriteLine(']');
                if (!mismatch) { mismatch = shiftCount != fileCount - 1; }
            }
        }

        if (mismatch) { return false; }
        return true;
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
    public bool ConvertFromV1 = false;
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
    bool INodeDeserializer.Deserialize(IParser parser, Type expectedType, Func<IParser, Type, object?> nestedObjectDeserializer, out object? value, ObjectDeserializer rootDeserializer)
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
