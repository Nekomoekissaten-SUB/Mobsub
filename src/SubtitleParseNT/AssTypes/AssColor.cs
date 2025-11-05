using System.Text;

namespace Mobsub.SubtitleParseNT2.AssTypes;

public class AssYCbCrMatrix : ICloneable
{
    private string matrix = "601";
    private readonly string[] matrixValid = ["None", "601", "709", "2020", "240M", "FCC"];
    public string Matrix
    {
        get => matrix;
        set
        {
            if (!matrixValid.Contains(value))
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

    public object Clone()
    {
        return MemberwiseClone();
    }
}

