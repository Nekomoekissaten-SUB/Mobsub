using System.Buffers.Text;
using System.IO;
using System.Text;

namespace Mobsub.SubtitleParse.AssTypes;

public sealed class AssAegisubExtradata
{
    public const char InlineStringType = 'e';

    public sealed record Entry(uint Id, string Key, string Value, char Type);

    private readonly Dictionary<uint, Entry> _entries = new();
    private uint _nextId = 1;

    public IReadOnlyDictionary<uint, Entry> Entries => _entries;

    public void Clear()
    {
        _entries.Clear();
        _nextId = 1;
    }

    public bool TryGet(uint id, out Entry entry) => _entries.TryGetValue(id, out entry!);

    public void Set(uint id, string key, string value, char type = InlineStringType)
    {
        _entries[id] = new Entry(id, key ?? string.Empty, value ?? string.Empty, type);
        if (id >= _nextId)
            _nextId = id + 1;
    }

    public bool Remove(uint id) => _entries.Remove(id);

    public uint GetOrCreateId(string key, string value, char type = InlineStringType)
    {
        key ??= string.Empty;
        value ??= string.Empty;

        foreach (var existing in _entries.Values)
        {
            if (string.Equals(existing.Key, key, StringComparison.Ordinal) &&
                string.Equals(existing.Value, value, StringComparison.Ordinal) &&
                (existing.Type == type || existing.Type == char.ToLowerInvariant(type) || existing.Type == char.ToUpperInvariant(type)))
            {
                return existing.Id;
            }
        }

        var id = _nextId++;
        _entries[id] = new Entry(id, key, value, type);
        return id;
    }

    public void Read(ReadOnlyMemory<byte> line, int lineNumber)
    {
        var spBytes = Utils.TrimSpaces(line.Span);
        if (spBytes.IsEmpty || spBytes[0] == (byte)';')
            return;

        var lineStr = Utils.GetString(spBytes);
        if (!TryParseExtradataLine(lineStr, out var entry))
            return;

        _entries[entry.Id] = entry;
        if (entry.Id >= _nextId)
            _nextId = entry.Id + 1;
    }

    public void WriteSection(StreamWriter sw, char[] newline)
    {
        WriteSection(sw, newline);
    }

    public void WriteSection(TextWriter writer, string newline)
    {
        writer.Write(AssConstants.SectionAegisubExtradata);
        writer.Write(newline);

        foreach (var e in _entries.Values.OrderBy(e => e.Id))
        {
            writer.Write("Data: ");
            writer.Write(e.Id);
            writer.Write(',');
            writer.Write(InlineStringEncode(e.Key));
            writer.Write(',');

            // Preserve the original encoding when possible.
            if (e.Type is 'u' or 'U')
            {
                writer.Write('u');
                writer.Write(UUEncode(Encoding.UTF8.GetBytes(e.Value ?? string.Empty)));
            }
            else
            {
                writer.Write('e');
                writer.Write(InlineStringEncode(e.Value ?? string.Empty));
            }

            writer.Write(newline);
        }
    }

    public void WriteSection(TextWriter writer, ReadOnlySpan<char> newline)
    {
        writer.Write(AssConstants.SectionAegisubExtradata);
        writer.Write(newline);

        foreach (var e in _entries.Values.OrderBy(e => e.Id))
        {
            writer.Write("Data: ");
            writer.Write(e.Id);
            writer.Write(',');
            writer.Write(InlineStringEncode(e.Key));
            writer.Write(',');

            // Preserve the original encoding when possible.
            if (e.Type is 'u' or 'U')
            {
                writer.Write('u');
                writer.Write(UUEncode(Encoding.UTF8.GetBytes(e.Value ?? string.Empty)));
            }
            else
            {
                writer.Write('e');
                writer.Write(InlineStringEncode(e.Value ?? string.Empty));
            }

            writer.Write(newline);
        }
    }

    private static bool TryParseExtradataLine(string line, out Entry entry)
    {
        entry = new Entry(0, string.Empty, string.Empty, 'e');

        if (!line.StartsWith("Data:", StringComparison.OrdinalIgnoreCase))
            return false;

        int pos = line.IndexOf(':');
        if (pos < 0)
            return false;
        pos++;
        while (pos < line.Length && char.IsWhiteSpace(line[pos]))
            pos++;

        int comma1 = line.IndexOf(',', pos);
        if (comma1 < 0)
            return false;

        if (!uint.TryParse(line.Substring(pos, comma1 - pos), out var id))
            return false;

        int comma2 = line.IndexOf(',', comma1 + 1);
        if (comma2 < 0 || comma2 + 1 >= line.Length)
            return false;

        string key = InlineStringDecode(line.Substring(comma1 + 1, comma2 - comma1 - 1));

        char type = line[comma2 + 1];
        string payload = comma2 + 2 < line.Length ? line.Substring(comma2 + 2) : string.Empty;
        string value = type switch
        {
            'e' or 'E' => InlineStringDecode(payload),
            'u' or 'U' => DecodeExtradataUu(payload),
            _ => string.Empty
        };

        entry = new Entry(id, key, value, type);
        return true;
    }

    private static string InlineStringDecode(string input)
    {
        if (string.IsNullOrEmpty(input))
            return string.Empty;

        var sb = new StringBuilder(input.Length);
        for (int i = 0; i < input.Length; i++)
        {
            char c = input[i];
            if (c != '#' || i + 2 >= input.Length)
            {
                sb.Append(c);
                continue;
            }

            int hi = HexValue(input[i + 1]);
            int lo = HexValue(input[i + 2]);
            if (hi < 0 || lo < 0)
            {
                sb.Append(c);
                continue;
            }

            sb.Append((char)((hi << 4) | lo));
            i += 2;
        }
        return sb.ToString();
    }

    private static string InlineStringEncode(string input)
    {
        if (string.IsNullOrEmpty(input))
            return string.Empty;

        var sb = new StringBuilder(input.Length);
        foreach (char c in input)
        {
            if (c <= 0x1F || c == '#' || c == ',' || c == ':' || c == '|')
            {
                sb.Append('#');
                sb.Append(((int)c & 0xFF).ToString("X2"));
            }
            else
            {
                sb.Append(c);
            }
        }
        return sb.ToString();
    }

    private static int HexValue(char c)
    {
        if ((uint)(c - '0') <= 9)
            return c - '0';
        if ((uint)(c - 'A') <= 5)
            return c - 'A' + 10;
        if ((uint)(c - 'a') <= 5)
            return c - 'a' + 10;
        return -1;
    }

    private static string DecodeExtradataUu(string input)
    {
        var bytes = UUDecode(input.AsSpan());
        return Encoding.UTF8.GetString(bytes);
    }

    private static byte[] UUDecode(ReadOnlySpan<char> input)
    {
        var result = new List<byte>(input.Length * 3 / 4);
        Span<byte> src = stackalloc byte[4];

        int pos = 0;
        while (pos < input.Length)
        {
            int n = 0;
            src.Clear();
            while (n < 4 && pos < input.Length)
            {
                char c = input[pos++];
                if (c == '\r' || c == '\n')
                    continue;
                src[n++] = (byte)(c - 33);
            }

            if (n > 1)
                result.Add((byte)((src[0] << 2) | (src[1] >> 4)));
            if (n > 2)
                result.Add((byte)(((src[1] & 0xF) << 4) | (src[2] >> 2)));
            if (n > 3)
                result.Add((byte)(((src[2] & 0x3) << 6) | src[3]));
        }

        return result.ToArray();
    }

    private static string UUEncode(ReadOnlySpan<byte> input)
    {
        if (input.IsEmpty)
            return string.Empty;

        var sb = new StringBuilder((input.Length + 2) / 3 * 4);
        int i = 0;
        while (i < input.Length)
        {
            byte a = input[i++];
            byte b = i < input.Length ? input[i++] : (byte)0;
            byte c = i < input.Length ? input[i++] : (byte)0;

            byte x0 = (byte)((a >> 2) & 0x3F);
            byte x1 = (byte)(((a << 4) | (b >> 4)) & 0x3F);
            byte x2 = (byte)(((b << 2) | (c >> 6)) & 0x3F);
            byte x3 = (byte)(c & 0x3F);

            sb.Append((char)(x0 + 33));
            sb.Append((char)(x1 + 33));
            sb.Append((char)(x2 + 33));
            sb.Append((char)(x3 + 33));
        }

        return sb.ToString();
    }
}
