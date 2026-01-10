using System.Text;
using Mobsub.SubtitleParseNT2.AssUtils;

namespace Mobsub.SubtitleParseNT2.AssTypes;

public enum AssEmbeddedFileType
{
    Unknown,
    Font,
    Graphics
}

public struct AssEmbeddedFile
{
    public string Name { get; set; }
    public string OriginalName { get; set; }
    public AssEmbeddedFileType FileType { get; set; }
    
    // Stores raw uuencoded lines to avoid allocation/decoding until needed
    public List<ReadOnlyMemory<byte>> Data { get; private set; }

    public AssEmbeddedFile(string name, string originalName, AssEmbeddedFileType type)
    {
        Name = name;
        OriginalName = originalName;
        FileType = type;
        Data = new List<ReadOnlyMemory<byte>>();
    }

    public byte[] GetDecodedData()
    {
        int actualEncodedLen = 0;
        for (var i = 0; i < Data.Count; i++)
        {
            // We need to trim spaces/newlines? 
            // AssData only strips newline characters (\r, \n).
            // Embedded data lines shouldn't have leading/trailing spaces usually.
            // But Utils.TrimSpaces can be safe.
            var span = Utils.TrimSpaces(Data[i].Span);
            if (span.IsEmpty) continue;

            if (i != Data.Count - 1 && span.Length != 80)
            {
                // Mimic original strict check
                throw new Exception($"Embedded data is broken! Line {i} length is {span.Length} (expected 80)");
            }

            actualEncodedLen += span.Length;
        }

        if (actualEncodedLen == 0)
            return Array.Empty<byte>();

        var expectedLen = (int)Math.Truncate(actualEncodedLen * 3 / 4d);
        if (expectedLen == 0)
            return Array.Empty<byte>();

        var decoded = new byte[expectedLen];
        int offset = 0;

        for (var i = 0; i < Data.Count; i++)
        {
            var span = Utils.TrimSpaces(Data[i].Span);
            if (span.IsEmpty) continue;

            DecodeChars(span, decoded, ref offset);
            if (offset >= decoded.Length)
                break;
        }

        if (offset < decoded.Length)
            return decoded.AsSpan(0, offset).ToArray();

        return decoded;
    }

    public void Encode(ReadOnlySpan<byte> sourceData)
    {
        Data.Clear();
        var sb = new StringBuilder(80);
        var buffer = new byte[3];
        var len = sourceData.Length;
        var pos = 0;

        while (pos < len)
        {
            var remain = len - pos;
            var readLen = remain >= 3 ? 3 : remain;
            
            // buffer[0] = sourceData[pos]; etc
            // But we need to be careful with range.
            
            if (readLen == 3)
            {
                EncodeChar3(sourceData.Slice(pos, 3), sb);
                pos += 3;
            }
            else if (readLen == 2)
            {
                EncodeChar2(sourceData.Slice(pos, 2), sb);
                pos += 2;
            }
            else // 1
            {
                EncodeChar1(sourceData.Slice(pos, 1), sb);
                pos += 1;
            }

            if (sb.Length >= 80)
            {
                AddLine(sb);
            }
        }
        if (sb.Length > 0)
        {
            AddLine(sb);
        }
    }

    private void AddLine(StringBuilder sb)
    {
        // Store as bytes (ASCII)
        var bytes = Encoding.ASCII.GetBytes(sb.ToString()); // Allocation here inevitably
        Data.Add(bytes);
        sb.Clear();
    }

   private static void EncodeChar1(ReadOnlySpan<byte> buffer, StringBuilder sb)
    {
        sb.Append((char)(((buffer[0] >> 2) & 0x3f) + 33));
        sb.Append((char)(((buffer[0] << 4) & 0x3f) + 33));
    }

    private static void EncodeChar2(ReadOnlySpan<byte> buffer, StringBuilder sb)
    {
        sb.Append((char)(((buffer[0] >> 2) & 0x3f) + 33));
        sb.Append((char)(((buffer[0] << 4) & 0x3f | (buffer[1] >> 4) & 0x0f) + 33));
        sb.Append((char)(((buffer[1] << 2) & 0x3f) + 33));
    }

    private static void EncodeChar3(ReadOnlySpan<byte> buffer, StringBuilder sb)
    {
        sb.Append((char)(((buffer[0] >> 2) & 0x3f) + 33));
        sb.Append((char)(((buffer[0] << 4) & 0x3f | (buffer[1] >> 4) & 0x0f) + 33));
        sb.Append((char)(((buffer[1] << 2) & 0x3f | (buffer[2] >> 6) & 0x03) + 33));
        sb.Append((char)(((buffer[2] << 0) & 0x3f) + 33));
    }

    private static void DecodeChars(ReadOnlySpan<byte> s, byte[] buffer, ref int offset)
    {
        if (offset >= buffer.Length)
            return;

        for (var i = 0; i < s.Length; i += 4)
        {
            if (offset >= buffer.Length)
                return;

            var r = (i + 4 <= s.Length) ? s.Slice(i, 4) : s[i..];
            
            int n0 = (r.Length > 0 ? r[0] : 33) - 33;
            int n1 = (r.Length > 1 ? r[1] : 33) - 33;
            int n2 = (r.Length > 2 ? r[2] : 33) - 33;
            int n3 = (r.Length > 3 ? r[3] : 33) - 33;

            var b0 = (byte)((n0 << 2) & 0xff | (n1 >> 4) & 0x03);
            var b1 = (byte)((n1 << 4) & 0xff | (n2 >> 2) & 0x0f);
            var b2 = (byte)((n2 << 6) & 0xff | (n3 >> 0) & 0x3f);

            if (offset < buffer.Length) buffer[offset++] = b0;
            if (offset < buffer.Length) buffer[offset++] = b1;
            if (offset < buffer.Length) buffer[offset++] = b2;
        }
    }
}

public class AssEmbeddedSection
{
    public List<AssEmbeddedFile> Files { get; } = new();
    private AssEmbeddedFile? _currentFile;
    private readonly AssEmbeddedFileType _sectionType;

    public AssEmbeddedSection(AssEmbeddedFileType type)
    {
        _sectionType = type;
    }

    public void Read(ReadOnlyMemory<byte> line, int lineNumber)
    {
        var span = line.Span;
        if (span.IsEmpty) return;

        // Check for header
        // Fonts: "fontname: "
        // Graphics: "filename: "
        if (span.IndexOf((byte)':') is int idx && idx > 0)
        {
            var key = span[..idx];
            bool isHeader = false;

            if (_sectionType == AssEmbeddedFileType.Font && key.SequenceEqual("fontname"u8))
            {
                isHeader = true;
            }
            else if (_sectionType == AssEmbeddedFileType.Graphics && key.SequenceEqual("filename"u8))
            {
                isHeader = true;
            }

            if (isHeader)
            {
                if (_currentFile.HasValue)
                {
                    Files.Add(_currentFile.Value);
                }

                var valueSpan = Utils.TrimSpaces(span[(idx + 1)..]);
                var name = Utils.GetString(valueSpan); // Usually ASCII/UTF8
                // OriginalName might be same or processed
                _currentFile = new AssEmbeddedFile(name, name, _sectionType);
                return;
            }
        }

        // It's data (uuencoded)
        if (_currentFile.HasValue)
        {
            // We just store the view. 
            // NOTE: AssData buffer must be kept alive! (It is)
            _currentFile.Value.Data.Add(line);
        }
    }
    
    public void Finish()
    {
        if (_currentFile.HasValue)
        {
            Files.Add(_currentFile.Value);
            _currentFile = null;
        }
    }
}
