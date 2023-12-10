// Copyright (c) 2022 CXuesong
// SPDX-License-Identifier: Apache-2.0
// Modifications copyright (c) MIR

using OpenCCSharp.Conversion;
using TriesSharp.Collections;

namespace Mobsub.ZhConvert;

public partial class OpenCCSharpUtils
{
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
}