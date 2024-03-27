using System.Text;

namespace Mobsub.SubtitleParse.AssTypes;

public class AssYCbCrMatrix
{
    private string matrix = "601";
    private readonly string[] matrixVaild = ["None", "601", "709", "2020", "240M", "FCC"];
    public string Matrix
    {
        get => matrix;
        set
        {
            if (!matrixVaild.Contains(value))
            {
                throw new ArgumentException($"YCbCr Matrix: {value} should be valid");
            }
            matrix = value;
        }
    }
    public bool Full = false;   // full-range (true) or tv-range (false)
    public AssYCbCrMatrix()
    {
        Matrix = "601";
        Full = false;   // TV, PC
    }

    public StringBuilder ToStringBuilder()
    {
        var sb = new StringBuilder();
        if (Matrix.AsSpan() == "None".AsSpan())
        {
            sb.Append(Matrix);
        }
        else
        {
            sb.Append(Full ? "PC" : "TV");
            sb.Append('.');
            sb.Append(Matrix);
        }
        return sb;
    }
}

public struct AssRGB8(byte red, byte green, byte blue, byte alpha)
{
    public byte R = red;
    public byte G = green;
    public byte B = blue;
    public byte A = alpha;

    public AssRGB8 Parse(ReadOnlySpan<char> sp)
    {
        var sign = (sp[^1] == '&') ? 3 : 2;

        if ((sp[0] != '&') || (sp[1] != 'H') || ((sp.Length - sign) % 2 != 0))
        {
            throw new Exception($"Invaild color: {sp}");
        }

        var loop = 0;
        for (int i = sp.Length - sign + 1; i > 1; i -= 2)
        {
            var n = Convert.ToByte(HexCharToInt(sp[i-1]) * 16 + HexCharToInt(sp[i]));
            
            switch (loop)
            {
                case 0:
                    R = n;
                    break;
                case 1:
                    G = n;
                    break;
                case 2:
                    B = n;
                    break;
                case 3:
                    A = n;
                    break;
                default:
                    throw new Exception($"Invaild color: {sp}");
            }

            loop += 1;
        }

        return this;
    }

    public readonly string ConvetToString(bool alpha)
    {
        var bytel = new List<byte>();
        if (alpha)
        {
            bytel.Add(A);
        }
        bytel.Add(B);
        bytel.Add(G);
        bytel.Add(R);

        var str = Convert.ToHexString(bytel.ToArray());

        return str;
    }
    public static int HexCharToInt(char c)
    {
        if (c >= 'A' && c <= 'F')
        {
            return c - 55;
        }
        else if (c >= 'a' && c <= 'f')
        {
            return c - 87;
        }
        else if (c >= '0' && c <= '9')
        {
            return c - 48;
        }
        else
        {
            throw new Exception($"Invaild char: {c}");
        }
    }


}