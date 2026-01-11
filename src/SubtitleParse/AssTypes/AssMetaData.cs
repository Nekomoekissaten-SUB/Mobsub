using System.Collections.Generic;
using Mobsub.SubtitleParse.AssUtils;

namespace Mobsub.SubtitleParse.AssTypes;

public class AssMetaData
{
    public Dictionary<string, string> Data { get; } = new(StringComparer.OrdinalIgnoreCase);

    public void Read(ReadOnlyMemory<byte> line, int lineNumber)
    {
        var spBytes = Utils.TrimSpaces(line.Span);
        if (spBytes.IsEmpty || spBytes[0] == (byte)';') return;

        var lineStr = Utils.GetString(spBytes).AsSpan();
        
        // Try to split by first colon
        if (Utils.TrySplitKeyValue(lineStr, out var k, out var v))
        {
            // Handle duplicate keys? Usually overwrites or ignores.
            // For garbage/extradata, usually unique keys.
            if (!Data.ContainsKey(k))
            {
                Data[k] = v;
            }
        }
        else
        {
             // Line without colon? Store as key with empty value or ignore?
             // Some formats might have continuation lines but Aegisub usually writes KV single lines.
             // We'll store it as key=line, value=""
             // We'll store it as key=line, value=""
             var s = lineStr.ToString();
             if (!Data.ContainsKey(s))
             {
                 Data[s] = string.Empty;
             }
        }
    }
}
