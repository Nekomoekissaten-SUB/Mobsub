using System.Buffers;

namespace Mobsub.SubtitleParse.Language;

/// <summary>
/// Maps between UTF-8 byte indices (of a line encoded with Encoding.UTF8 replacement fallback)
/// and UTF-16 char indices (of the original string).
/// </summary>
internal struct Utf8IndexMap : IDisposable
{
    private int[]? _charToByte;
    private int _charLength;
    private int _byteLength;

    public int CharLength => _charLength;
    public int ByteLength => _byteLength;

    public static Utf8IndexMap Create(ReadOnlySpan<char> text)
    {
        int charLength = text.Length;
        int[] map = ArrayPool<int>.Shared.Rent(charLength + 1);

        int byteCount = 0;
        map[0] = 0;

        for (int i = 0; i < charLength; i++)
        {
            char c = text[i];

            if (char.IsHighSurrogate(c) && i + 1 < charLength && char.IsLowSurrogate(text[i + 1]))
            {
                // Surrogate pair => always 4 bytes in UTF-8.
                map[i + 1] = byteCount;
                byteCount += 4;
                map[i + 2] = byteCount;
                i++; // consume low surrogate
                continue;
            }

            int bytes;
            if (c <= 0x7F)
            {
                bytes = 1;
            }
            else if (c <= 0x7FF)
            {
                bytes = 2;
            }
            else
            {
                // Includes unpaired surrogates: Encoding.UTF8 replacement fallback emits U+FFFD (3 bytes).
                bytes = 3;
            }

            byteCount += bytes;
            map[i + 1] = byteCount;
        }

        return new Utf8IndexMap
        {
            _charToByte = map,
            _charLength = charLength,
            _byteLength = byteCount
        };
    }

    public int ByteToCharIndex(int byteIndex)
    {
        if (byteIndex <= 0)
            return 0;
        if (byteIndex >= _byteLength)
            return _charLength;

        var map = _charToByte!;

        int lo = 0;
        int hi = _charLength;

        while (lo <= hi)
        {
            int mid = lo + ((hi - lo) >> 1);
            int v = map[mid];

            if (v == byteIndex)
            {
                // Prefer the leftmost match when duplicates exist (surrogate-pair boundary).
                while (mid > 0 && map[mid - 1] == byteIndex)
                    mid--;
                return mid;
            }

            if (v < byteIndex)
                lo = mid + 1;
            else
                hi = mid - 1;
        }

        // Not an exact boundary: return the preceding char index.
        return hi < 0 ? 0 : hi;
    }

    public void Dispose()
    {
        if (_charToByte == null)
            return;

        ArrayPool<int>.Shared.Return(_charToByte);
        _charToByte = null;
        _charLength = 0;
        _byteLength = 0;
    }
}

