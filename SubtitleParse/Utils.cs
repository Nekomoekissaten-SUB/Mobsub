using System.Text;
using System.Runtime.InteropServices;

namespace Mobsub.Utils;

class DetectEncoding
{
    public static Encoding GuessEncoding(byte[] buffer)
    {
        if (buffer.Length >= 2 && buffer[0] == 0xFF && buffer[1] == 0xFE)
        {
            // UTF-16 (Little-Endian)
            return Encoding.Unicode;
        }
        else if (buffer.Length >= 2 && buffer[0] == 0xFE && buffer[1] == 0xFF)
        {
            // UTF-16 (Big-Endian)
            return Encoding.BigEndianUnicode;
        }
        else if (buffer.Length >= 3 && buffer[0] == 0xEF && buffer[1] == 0xBB && buffer[2] == 0xBF)
        {
            // UTF-8
            return Encoding.UTF8;
        }
        else
        {
            // Default to UTF-8 without Bom
            return new UTF8Encoding(false);
        }
    }

    public static Encoding EncodingRefOS()
    {
        switch (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            case false:
                return new UTF8Encoding(false);
            default:
                return Encoding.UTF8;
        }
    }

}