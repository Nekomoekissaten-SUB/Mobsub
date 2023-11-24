using Mobsub.AssTypes;
using System.Text;

namespace Mobsub.SubtitleParse;

public class AssEmbededParse
{
    public static void UUEncode(BinaryReader br, List<string> data, ref int len)
    {
        var sb = new StringBuilder(80);

        var buffer = new byte[3];
        while (br.BaseStream.Position < br.BaseStream.Length)
        {
            int readLength = br.Read(buffer, 0, 3);

            switch (readLength)
            {
                case 1:
                    EncodeChar1(buffer, sb);
                    data.Add(sb.ToString());
                    len += sb.Length;
                    sb.Clear();
                    break;
                case 2:
                    EncodeChar2(buffer, sb);
                    data.Add(sb.ToString());
                    len += sb.Length;
                    sb.Clear();
                    break;
                case 3:
                    EncodeChar3(buffer, sb);
                    if (sb.Length == 80)
                    {
                        data.Add(sb.ToString());
                        sb.Clear();
                        len += 80;
                    }
                    break;
            }
        }
    }

    public static void UUDecode(string[] data, int length, MemoryStream memStream)
    {
        var orgLen = (int)Math.Truncate(length * 3 / 4d);

        for (var i = 0; i < data.Length; i++)
        {
            var s = data[i].AsSpan();
            if (i != data.Length - 1 && s.Length != 80)
            {
                throw new Exception("Embedded data is broken!");
            }
            DecodeChars(s, memStream);
        }
        memStream.SetLength(orgLen);
    }

    private static void EncodeChar1(byte[] buffer, StringBuilder sb)
    {
        sb.Append((char)(((buffer[0] >> 2) & 0x3f) + 33));
        sb.Append((char)(((buffer[0] << 4) & 0x3f) + 33));
    }

    private static void EncodeChar2(byte[] buffer, StringBuilder sb)
    {
        sb.Append((char)(((buffer[0] >> 2) & 0x3f) + 33));
        sb.Append((char)(((buffer[0] << 4) & 0x3f | (buffer[1] >> 4) & 0x0f) + 33));
        sb.Append((char)(((buffer[1] << 2) & 0x3f) + 33));
    }

    private static void EncodeChar3(byte[] buffer, StringBuilder sb)
    {
        sb.Append((char)(((buffer[0] >> 2) & 0x3f) + 33));
        sb.Append((char)(((buffer[0] << 4) & 0x3f | (buffer[1] >> 4) & 0x0f) + 33));
        sb.Append((char)(((buffer[1] << 2) & 0x3f | (buffer[2] >> 6) & 0x03) + 33));
        sb.Append((char)(((buffer[2] << 0) & 0x3f) + 33));
    }

    private static void DecodeChars(ReadOnlySpan<char> s, MemoryStream memStream)
    {
        for (var i = 0; i < s.Length; i += 4)
        {
            var r = (i + 4 <= s.Length) ? s.Slice(i, 4) : s[i..];

            var na = new int[4];

            for (var j = 0; j < r.Length; j++)
            {
                na[j] = r[j] - 33;
            }
            
            var buffer = new byte[3];
            buffer[0] = (byte)((na[0] << 2) & 0xff | (na[1] >> 4) & 0x03);
            buffer[1] = (byte)((na[1] << 4) & 0xff | (na[2] >> 2) & 0x0f);
            buffer[2] = (byte)((na[2] << 6) & 0xff | (na[3] >> 0) & 0x3f);

            memStream.Write(buffer);
        }
    }

    private static void WriteFile(string[] data, int length, string filePath)
    {
        using var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write);
        using var ms = new MemoryStream();
        UUDecode(data, length, ms);
        ms.Seek(0, SeekOrigin.Begin);
        ms.CopyTo(fs);
        fs.Close();
    }

    public static void WriteFontFile(AssEmbeddedFont embFont, DirectoryInfo dirPath)
    {
        var filePath = Path.Combine(dirPath.FullName, $"{embFont.OriginalName}{embFont.Suffix}");
        WriteFontFile(embFont, filePath, true);
    }

    public static void WriteFontFile(AssEmbeddedFont embFont, string filePath, bool printInfo)
    {
        WriteFile([.. embFont.Data], embFont.DataLength, filePath);

        if (printInfo)
        {
            var sb = new StringBuilder("Info: ");
            sb.Append(embFont.OriginalName);
            sb.Append(embFont.Suffix);
            if (embFont.Bold)
            {
                sb.Append(", Bold");
            }
            if (embFont.Italic)
            {
                sb.Append(", Italic");
            }
            if (embFont.CharacterEncoding > 0)
            {
                sb.Append($", Character Encoding: {embFont.CharacterEncoding}");
            }
            Console.WriteLine(sb);
        }
    }

    public static void WriteGraphicFile(AssEmbeddedGraphic embGraphic, string filePath, bool printInfo)
    {
        WriteFile([.. embGraphic.Data], embGraphic.DataLength, filePath);
        if (printInfo)
        {
            Console.WriteLine($"Extract: {embGraphic.Name}");
        }
    }

}