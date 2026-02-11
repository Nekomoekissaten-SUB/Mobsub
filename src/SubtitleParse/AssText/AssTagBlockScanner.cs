using Mobsub.SubtitleParse.AssTypes;
using System.Runtime.CompilerServices;

namespace Mobsub.SubtitleParse.AssText;

internal readonly ref struct AssTagBlockToken
{
    public int TagStart { get; }
    public int TagEnd { get; }

    public int NameStart { get; }
    public int NameEnd { get; }

    public int ParamStart { get; }
    public int ParamEnd { get; }

    public bool IsKnown { get; }
    public AssTag Tag { get; }
    public int MatchedLength { get; }

    public ReadOnlySpan<byte> NameAndMaybePayload { get; }
    public ReadOnlySpan<byte> Param { get; }
    public ReadOnlyMemory<byte> ParamMemory { get; }

    public AssTagBlockToken(
        int tagStart,
        int tagEnd,
        int nameStart,
        int nameEnd,
        int paramStart,
        int paramEnd,
        bool isKnown,
        AssTag tag,
        int matchedLength,
        ReadOnlySpan<byte> nameAndMaybePayload,
        ReadOnlySpan<byte> param,
        ReadOnlyMemory<byte> paramMemory)
    {
        TagStart = tagStart;
        TagEnd = tagEnd;
        NameStart = nameStart;
        NameEnd = nameEnd;
        ParamStart = paramStart;
        ParamEnd = paramEnd;
        IsKnown = isKnown;
        Tag = tag;
        MatchedLength = matchedLength;
        NameAndMaybePayload = nameAndMaybePayload;
        Param = param;
        ParamMemory = paramMemory;
    }
}

internal ref struct AssTagBlockScanner
{
    private readonly ReadOnlySpan<byte> _block;
    private readonly int _absoluteStart;
    private readonly ReadOnlyMemory<byte> _lineMemory;
    private int _i;

    public AssTagBlockScanner(ReadOnlySpan<byte> block, int absoluteStart, ReadOnlyMemory<byte> lineMemory)
    {
        _block = block;
        _absoluteStart = absoluteStart;
        _lineMemory = lineMemory;
        _i = 0;
    }

    public bool MoveNext(out AssTagBlockToken token)
    {
        var block = _block;
        int i = _i;

        while ((uint)i < (uint)block.Length)
        {
            int tagStartOffset = block[i..].IndexOf((byte)'\\');
            if (tagStartOffset < 0)
            {
                _i = block.Length;
                token = default;
                return false;
            }

            i += tagStartOffset;
            int tagStart = i;

            i++; // skip '\\'
            if ((uint)i >= (uint)block.Length)
            {
                _i = block.Length;
                token = default;
                return false;
            }

            int nameStart = i;
            if (!IsAsciiLetterOrDigit(block[nameStart]))
            {
                i = nameStart + 1;
                continue;
            }

            while ((uint)i < (uint)block.Length && IsAsciiLetterOrDigit(block[i]))
                i++;

            int nameEnd = i;
            int nameLen = nameEnd - nameStart;
            if (nameLen <= 0)
                continue;

            var nameAndMaybePayload = block.Slice(nameStart, nameLen);

            int nextBackslash = block[i..].IndexOf((byte)'\\');
            int paramEndCandidate = nextBackslash < 0 ? block.Length : i + nextBackslash;

            if (AssTagRegistry.TryMatch(nameAndMaybePayload, out var tag, out int matchedLength))
            {
                AssTagRegistry.TryGetTagKind(tag, out var tagKind);
                bool shouldBeFunction = (tagKind & AssTagKind.ShouldBeFunction) != 0;

                int actualParamStart = nameStart + matchedLength;
                int paramStart = actualParamStart;
                int paramEnd;

                int parenStart = actualParamStart;
                if (shouldBeFunction)
                {
                    while ((uint)parenStart < (uint)block.Length && block[parenStart] == (byte)' ')
                        parenStart++;
                }

                if (shouldBeFunction && (uint)parenStart < (uint)block.Length && block[parenStart] == (byte)'(')
                {
                    int j = parenStart + 1;
                    int depth = 1;
                    var search = block[j..];

                    while (!search.IsEmpty && depth > 0)
                    {
                        int braceIndex = search.IndexOfAny((byte)'(', (byte)')');
                        if (braceIndex == -1)
                        {
                            j = block.Length;
                            break;
                        }

                        j = (int)(block.Length - search.Length) + braceIndex;
                        if (block[j] == (byte)'(') depth++;
                        else depth--;

                        j++;
                        search = block[j..];
                    }

                    paramEnd = j;
                    i = j;
                }
                else
                {
                    paramEnd = paramEndCandidate;
                    i = paramEnd;
                }

                int paramLength = Math.Max(0, paramEnd - paramStart);
                var paramSpan = paramLength == 0 ? ReadOnlySpan<byte>.Empty : block.Slice(paramStart, paramLength);
                ReadOnlyMemory<byte> paramMemory = default;
                if (!_lineMemory.IsEmpty)
                    paramMemory = _lineMemory.Slice(_absoluteStart + paramStart, paramLength);

                token = new AssTagBlockToken(
                    tagStart: _absoluteStart + tagStart,
                    tagEnd: _absoluteStart + paramEnd,
                    nameStart: _absoluteStart + nameStart,
                    nameEnd: _absoluteStart + nameEnd,
                    paramStart: _absoluteStart + paramStart,
                    paramEnd: _absoluteStart + paramEnd,
                    isKnown: true,
                    tag: tag,
                    matchedLength: matchedLength,
                    nameAndMaybePayload: nameAndMaybePayload,
                    param: paramSpan,
                    paramMemory: paramMemory);

                _i = i;
                return true;
            }

            // Unknown tag: treat param as bytes until next backslash.
            int unknownParamStart = nameEnd;
            int unknownParamEnd = paramEndCandidate;
            int unknownParamLength = Math.Max(0, unknownParamEnd - unknownParamStart);
            var unknownParam = unknownParamLength == 0 ? ReadOnlySpan<byte>.Empty : block.Slice(unknownParamStart, unknownParamLength);
            ReadOnlyMemory<byte> unknownParamMemory = default;
            if (!_lineMemory.IsEmpty)
                unknownParamMemory = _lineMemory.Slice(_absoluteStart + unknownParamStart, unknownParamLength);

            token = new AssTagBlockToken(
                tagStart: _absoluteStart + tagStart,
                tagEnd: _absoluteStart + unknownParamEnd,
                nameStart: _absoluteStart + nameStart,
                nameEnd: _absoluteStart + nameEnd,
                paramStart: _absoluteStart + unknownParamStart,
                paramEnd: _absoluteStart + unknownParamEnd,
                isKnown: false,
                tag: default,
                matchedLength: 0,
                nameAndMaybePayload: nameAndMaybePayload,
                param: unknownParam,
                paramMemory: unknownParamMemory);

            i = unknownParamEnd;
            _i = i;
            return true;
        }

        _i = i;
        token = default;
        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsAsciiLetterOrDigit(byte b)
        => (uint)(b - (byte)'0') <= 9 || (uint)((b | 0x20) - (byte)'a') <= 25;
}
