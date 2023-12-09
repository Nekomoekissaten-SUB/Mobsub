using OpenCCSharp.Conversion;
using TriesSharp.Collections;

namespace Mobsub.ZhConvert;

public partial class OpenCCSharpUtils
{
    public static ChainedScriptConverter GetConverter(List<List<string?>> conversionSteps)
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
                dicts.Add(GetDictionaryFrom(s));
            }
            var mergedMapping = new MergedStringPrefixMapping(dicts);
            var lexer = new LongestPrefixLexer(mergedMapping);
            var converter = new ScriptConverter(lexer, mergedMapping);
            converters.Add(converter);
        }
        return new ChainedScriptConverter(converters);
    }

    public static async ValueTask BuildTriesDictionary(FileInfo textFile, FileInfo target)
    {
        var dict = new TrieStringPrefixDictionary();
        
        await using var isr = new FileStream(textFile.FullName, FileMode.Open, FileAccess.Read, FileShare.Read, 4096,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        await foreach (var kv in PlainTextConversionLookupTable.EnumEntriesFromAsync(isr))
        {
            var m = GC.AllocateUninitializedArray<char>(kv.Value[0].Length).AsMemory();
            kv.Value[0].CopyTo(m);
            dict.TryAdd(kv.Key, m);
        }
        await using var osr = new FileStream(target.FullName, FileMode.Create, FileAccess.Write, FileShare.Read, 4096,
            FileOptions.Asynchronous | FileOptions.RandomAccess);
        await TrieSerializer.Serialize(osr, dict.Trie);
        await osr.FlushAsync();
    }

    public static TrieStringPrefixDictionary GetDictionaryFrom(string dictFileName)
    {
        var sr = new FileStream(dictFileName, FileMode.Open, FileAccess.Read);
        var trie = TrieSerializer.Deserialize(sr).AsTask().GetAwaiter().GetResult();
        var dict = new TrieStringPrefixDictionary(trie);
        return dict;
    }
}