using OpenCCSharp.Conversion;
using TriesSharp.Collections;

namespace Mobsub.Helper.ZhConvert;

public partial class OpenCCSharpUtils
{
    public static ChainedScriptConverter GetConverter(List<string?[]> conversionSteps)
    {
        List<ScriptConverter> converters = [];
        List<TrieStringPrefixDictionary> dicts = [];
        foreach (var l in conversionSteps)
        {
            foreach (var s in l)
            {
                if (s is null)
                {
                    continue;
                }
                dicts.Add(GetDictionaryFrom(s).AsTask().GetAwaiter().GetResult());
            }
            var mergedMapping = new MergedStringPrefixMapping(dicts);
            var lexer = new LongestPrefixLexer(mergedMapping);
            var converter = new ScriptConverter(lexer, mergedMapping);
            converters.Add(converter);
            dicts.Clear();
        }
        return new ChainedScriptConverter(converters);
    }

    public static async ValueTask<TrieStringPrefixDictionary> GetDictionaryFrom(string dictFileName)
    {
        await using var sr = new FileStream(dictFileName, FileMode.Open, FileAccess.Read, FileShare.None, 4096, FileOptions.Asynchronous | FileOptions.SequentialScan);
        var trie = await TrieSerializer.Deserialize(sr);
        var dict = new TrieStringPrefixDictionary(trie);
        return dict;
    }
}