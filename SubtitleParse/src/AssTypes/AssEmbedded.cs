using Microsoft.Extensions.Logging;
using ZLogger;
using System.Text;

namespace Mobsub.SubtitleParse.AssTypes;

public class AssEmbedded
{
    public class Font(ILogger<AssData>? logger = null)
    {
        public string OriginalName = string.Empty;  // lowercase
        public bool Bold = false;
        public bool Italic = false;
        public int CharacterEncoding = 0;
        public readonly string Suffix = ".ttf";
        public List<string> Data = [];
        public int DataLength = 0;

        private readonly ILogger<AssData>? _logger = logger;

        // public void EncodeFont(FileInfo file)
        // {
        //     if (!file.Exists)
        //     {
        //         throw new IOException($"{file.FullName} not exists");
        //     }

        //     // record font name?
        // }

        public void Write(StreamWriter sw, char[] newline)
        {
            var sb = new StringBuilder($"filename: {OriginalName}_");

            var info = new List<char>();
            if (Bold)
            {
                info.Add('B');
            }
            if (Italic)
            {
                info.Add('I');
            }
            info.Add((char)(CharacterEncoding + '0'));
            sb.Append(info);
            sw.Write($"filename: {sb}");
            sw.Write(newline);
            for (int i = 0; i < Data.Count; i++)
            {
                sw.Write(Data.ToArray()[i]);
                sw.Write(newline);
            }
        }

        public void WriteFile(DirectoryInfo dirPath)
        {
            var filePath = Path.Combine(dirPath.FullName, $"{OriginalName}{Suffix}");
            WriteFile(filePath);
        }

        public void WriteFile(string filePath)
        {
            _logger?.ZLogInformation($"Extract font info: {OriginalName}{Suffix}{(Bold ? ", Bold" : string.Empty)}{(Italic ? ", Italic" : string.Empty)}{(CharacterEncoding > 0 ? $", Character Encoding: {CharacterEncoding}" : string.Empty)}");
            AssEmbedded.WriteFile([.. Data], DataLength, filePath);
            _logger?.ZLogInformation($"Extract fine");
        }
    }

    public class Graphic(ILogger<AssData>? logger = null)
    {
        public string Name = string.Empty;  // lowercase
        public List<string> Data = [];
        public int DataLength = 0;

        private readonly ILogger<AssData>? _logger = logger;

        public void EncodeGraphic(FileInfo file)
        {
            if (!file.Exists)
            {
                throw new IOException($"{file.FullName} not exists");
            }

            Name = file.Name;
            using var fs = new FileStream(file.FullName, FileMode.Open, FileAccess.Read);
            var br = new BinaryReader(fs);
            UUEncode(br, Data, ref DataLength);
            fs.Close();
        }

        public void Write(StreamWriter sw, char[] newline)
        {
            sw.Write($"filename: {Name}");
            sw.Write(newline);
            for (int i = 0; i < Data.Count; i++)
            {
                sw.Write(Data.ToArray()[i]);
                sw.Write(newline);
            }
        }

        public void WriteFile(string filePath)
        {
            _logger?.ZLogInformation($"Extract {Name}");
            AssEmbedded.WriteFile([..Data], DataLength, filePath);
            _logger?.ZLogInformation($"Extract fine");
        }

    }

    internal static List<Font> ParseFontsFromAss(ReadOnlySpan<char> sp, int lineNumber, ILogger<AssData>? _logger = null)
    {
        var fonts = new List<Font>();
        if (sp.StartsWith("fontname:"))
        {
            var eFont = new Font();
            var startIdx = "fontname:".Length + 1;  // fontname: chaucer_B0.ttf
            var lastSepIdx = sp.LastIndexOf('_');
            var lastSeg = sp[(lastSepIdx + 1)..];
            var noAddition = false;
            _logger?.ZLogInformation($"Start parse embedded font {lastSeg.ToString()} begin at line {lineNumber}");

            int encoding;
            var bPos = lastSeg.IndexOf('B');
            var iPos = lastSeg.IndexOf('I');
            if (bPos == 0)
            {
                eFont.Bold = true;
                if (iPos == 1)
                {
                    eFont.Italic = true;
                    if (int.TryParse(lastSeg[2..], out encoding))
                    {
                        eFont.CharacterEncoding = encoding;
                    }
                    else
                    {
                        noAddition = true;
                    }
                }
                else if (int.TryParse(lastSeg[1..], out encoding))
                {
                    eFont.CharacterEncoding = encoding;
                }
                else
                {
                    noAddition = true;
                }
            }
            else if (iPos == 0)
            {
                eFont.Italic = true;
                if (int.TryParse(lastSeg[1..], out encoding))
                {
                    eFont.CharacterEncoding = encoding;
                }
                else
                {
                    noAddition = true;
                }
            }
            else if (int.TryParse(lastSeg, out encoding))
            {
                eFont.CharacterEncoding = encoding;
            }
            else
            {
                noAddition = true;
            }

            eFont.OriginalName = noAddition ? sp[startIdx..].ToString() : sp[startIdx..lastSepIdx].ToString();

            fonts.Add(eFont);
        }
        else if (fonts.Count > 0)
        {
            var lastFont = fonts[^1];
            lastFont.Data.Add(sp.ToString());
            lastFont.DataLength += sp.Length;
        }
        
        return fonts;
    }

    internal static List<Graphic> ParseGraphicsFromAss(ReadOnlySpan<char> sp, int lineNumber, ILogger<AssData>? _logger = null)
    {
        var graphics = new List<Graphic>();
        if (sp.StartsWith("filename:"))
        {
            if (Utils.TrySplitKeyValue(sp, out var _, out var value))
                graphics.Add(new Graphic() { Name = value });
            else
                throw new Exception($"Please check {sp.ToString()}");
            _logger?.ZLogInformation($"Start parse embedded file {value} begin at line {lineNumber}");
        }
        else
        {
            graphics.Last().Data.Add(sp.ToString());
            graphics.Last().DataLength += sp.Length;
        }
        return graphics;
    }

    internal static void UUDecode(string[] data, int length, MemoryStream memStream)
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

    internal static void UUEncode(BinaryReader br, List<string> data, ref int len)
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

    private static void WriteFile(string[] data, int length, string filePath)
    {
        using var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write);
        using var ms = new MemoryStream();
        UUDecode(data, length, ms);
        ms.Seek(0, SeekOrigin.Begin);
        ms.CopyTo(fs);
        fs.Close();
    }
}
