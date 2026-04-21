namespace Mobsub.SubtitleParse.Language;

public sealed class TextLineMap
{
    private readonly int[] _lineStarts;

    public TextLineMap(string text)
    {
        var lineStarts = new List<int>(capacity: 128) { 0 };
        for (int i = 0; i < text.Length; i++)
        {
            if (text[i] == '\n')
            {
                int nextStart = i + 1;
                if (nextStart <= text.Length)
                    lineStarts.Add(nextStart);
            }
        }
        _lineStarts = lineStarts.ToArray();
    }

    internal TextLineMap(int[] lineStarts)
    {
        _lineStarts = lineStarts;
    }

    public int LineCount => _lineStarts.Length;

    public AssPosition GetPosition(int offset)
    {
        offset = Math.Max(offset, 0);

        int line = Array.BinarySearch(_lineStarts, offset);
        if (line < 0)
            line = ~line - 1;
        if (line < 0)
            line = 0;

        int character = offset - _lineStarts[line];
        if (character < 0)
            character = 0;

        return new AssPosition(line, character);
    }

    public int GetLineStartOffset(int line)
    {
        if (_lineStarts.Length == 0)
            return 0;
        if (line <= 0)
            return 0;
        if (line >= _lineStarts.Length)
            return _lineStarts[^1];
        return _lineStarts[line];
    }

    public ReadOnlySpan<char> GetLineSpan(string text, int line)
    {
        if (_lineStarts.Length == 0)
            return ReadOnlySpan<char>.Empty;

        line = Math.Clamp(line, 0, _lineStarts.Length - 1);
        int start = _lineStarts[line];
        int end = line + 1 < _lineStarts.Length ? _lineStarts[line + 1] : text.Length;
        int length = end - start;

        if (length > 0 && text[start + length - 1] == '\n')
            length--;
        if (length > 0 && text[start + length - 1] == '\r')
            length--;

        return text.AsSpan(start, length);
    }
}
