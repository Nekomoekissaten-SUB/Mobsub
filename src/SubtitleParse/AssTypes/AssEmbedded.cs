using System.Text;
using Mobsub.SubtitleParse.AssUtils;

namespace Mobsub.SubtitleParse.AssTypes;

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
        // libass-compatible decode:
        // Each 6-bit value is stored as (value + 33). Decoding is base64-like with no padding.
        int encodedLen = 0;
        for (int i = 0; i < Data.Count; i++)
        {
            ReadOnlySpan<byte> span = Utils.TrimSpaces(Data[i].Span);
            if (!span.IsEmpty)
                encodedLen += span.Length;
        }

        if (encodedLen == 0)
            return Array.Empty<byte>();

        if (encodedLen % 4 == 1)
            throw new Exception("Bad embedded font data size (mod 4 == 1).");

        int outputLen = checked((encodedLen / 4) * 3 + Math.Max(encodedLen % 4, 1) - 1);
        if (outputLen == 0)
            return Array.Empty<byte>();

        byte[] decoded = new byte[outputLen];
        int dst = 0;

        Span<byte> buf = stackalloc byte[4];
        int bufCount = 0;

        for (int i = 0; i < Data.Count; i++)
        {
            ReadOnlySpan<byte> span = Utils.TrimSpaces(Data[i].Span);
            if (span.IsEmpty)
                continue;

            for (int j = 0; j < span.Length; j++)
            {
                buf[bufCount++] = span[j];
                if (bufCount == 4)
                {
                    dst = DecodeCharsLibass(buf, decoded, dst, 4);
                    bufCount = 0;
                    if (dst >= decoded.Length)
                        return decoded;
                }
            }
        }

        if (bufCount == 2)
            _ = DecodeCharsLibass(buf, decoded, dst, 2);
        else if (bufCount == 3)
            _ = DecodeCharsLibass(buf, decoded, dst, 3);
        else if (bufCount != 0)
            throw new Exception("Bad embedded font data size.");

        return decoded;
    }

    public void Encode(ReadOnlySpan<byte> sourceData)
    {
        Data.Clear();
        var len = sourceData.Length;
        var pos = 0;
        if (len == 0)
            return;

        var lineBuffer = new byte[80];
        int lineLength = 0;

        while (pos < len)
        {
            var remain = len - pos;
            var readLen = remain >= 3 ? 3 : remain;

            int needed = readLen == 3 ? 4 : readLen == 2 ? 3 : 2;
            if (lineLength + needed > 80)
            {
                AddLine(lineBuffer, lineLength);
                lineLength = 0;
            }

            if (readLen == 3)
            {
                lineLength += EncodeChar3(sourceData.Slice(pos, 3), lineBuffer.AsSpan(lineLength));
                pos += 3;
            }
            else if (readLen == 2)
            {
                lineLength += EncodeChar2(sourceData.Slice(pos, 2), lineBuffer.AsSpan(lineLength));
                pos += 2;
            }
            else // 1
            {
                lineLength += EncodeChar1(sourceData.Slice(pos, 1), lineBuffer.AsSpan(lineLength));
                pos += 1;
            }

            if (lineLength == 80)
            {
                AddLine(lineBuffer, lineLength);
                lineLength = 0;
            }
        }
        if (lineLength > 0)
        {
            AddLine(lineBuffer, lineLength);
        }
    }

    private void AddLine(byte[] buffer, int length)
    {
        if (length <= 0)
            return;

        var bytes = new byte[length];
        Buffer.BlockCopy(buffer, 0, bytes, 0, length);
        Data.Add(bytes);
    }

    private static int EncodeChar1(ReadOnlySpan<byte> buffer, Span<byte> dest)
    {
        dest[0] = (byte)(((buffer[0] >> 2) & 0x3f) + 33);
        dest[1] = (byte)(((buffer[0] << 4) & 0x3f) + 33);
        return 2;
    }

    private static int EncodeChar2(ReadOnlySpan<byte> buffer, Span<byte> dest)
    {
        dest[0] = (byte)(((buffer[0] >> 2) & 0x3f) + 33);
        dest[1] = (byte)(((buffer[0] << 4) & 0x3f | (buffer[1] >> 4) & 0x0f) + 33);
        dest[2] = (byte)(((buffer[1] << 2) & 0x3f) + 33);
        return 3;
    }

    private static int EncodeChar3(ReadOnlySpan<byte> buffer, Span<byte> dest)
    {
        dest[0] = (byte)(((buffer[0] >> 2) & 0x3f) + 33);
        dest[1] = (byte)(((buffer[0] << 4) & 0x3f | (buffer[1] >> 4) & 0x0f) + 33);
        dest[2] = (byte)(((buffer[1] << 2) & 0x3f | (buffer[2] >> 6) & 0x03) + 33);
        dest[3] = (byte)(((buffer[2] << 0) & 0x3f) + 33);
        return 4;
    }

    private static int DecodeCharsLibass(ReadOnlySpan<byte> src, byte[] dst, int dstIndex, int cntIn)
    {
        uint value = 0;
        for (int i = 0; i < cntIn; i++)
            value |= (uint)(((src[i] - 33) & 63) << (6 * (3 - i)));

        dst[dstIndex++] = (byte)(value >> 16);
        if (cntIn >= 3)
            dst[dstIndex++] = (byte)((value >> 8) & 0xff);
        if (cntIn >= 4)
            dst[dstIndex++] = (byte)(value & 0xff);

        return dstIndex;
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
