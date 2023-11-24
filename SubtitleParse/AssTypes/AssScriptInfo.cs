using System.Text;

namespace Mobsub.AssTypes;

public class AssScriptInfo
{
    private readonly string[] scriptTypes = ["v4.00", "v4.00+", "v4.00++"];
    private string scriptType = "v4.00";
    private int? layoutResX = null;
    private int? layoutResY = null;
    private float timer = 100.0000f;
    private int? wrapStyle = null;
    
    public string ScriptType
    {
        get => scriptType;
        set
        {
            if (!scriptTypes.Contains(value))
            {
                throw new ArgumentException($"ScriptType: {value} should be valid");
            }
            scriptType = value;
        }
    }
    public string Title {get; set;} = string.Empty;
    public int PlayResX {get; set;} = 640;
    public int PlayResY {get; set;} = 480;
    public int LayoutResX
    {
        get => layoutResX == null ? PlayResX : (int)layoutResX;
        set => layoutResX = value;
    }
    public int LayoutResY
    {
        get => layoutResY == null ? PlayResY : (int)layoutResY;
        set => layoutResY = value;
    }
    public float Timer
    {
        get => timer;
        set => timer = (float)Math.Round(value, 4);
    }
    public int WrapStyle
    {
        get => wrapStyle == null ? 0 : (int)wrapStyle;
        set
        {
            if (scriptType == "v4.00")
            {
                throw new ArgumentException($"WrapStyle: ass / ssa version {scriptType} don’t support {value}");
            }
            wrapStyle = value;
        }
    }
    public bool ScaledBorderAndShadow {get; set;} = true; // yes or no
    public bool Kerning {get; set;} = false;    // yes or no, only libass, vsfilter is disabled
    public AssYCbCrMatrix YCbCrMatrix {get; set;} = new AssYCbCrMatrix();
    public List<string> Comment = [];
    public Dictionary<string, string> Others = [];
    public int status = 0;
    public HashSet<string> Orders = [];

    public void Write(StreamWriter sw, char[] newline)
    {
        sw.Write("[Script Info]");
        sw.Write(newline);
        
        foreach (var s in Comment)
        {
            sw.Write($"; {s}");
            sw.Write(newline);
        }

        foreach (var k in Orders)
        {
            switch (k)
            {
                case "Title":
                    sw.Write($"Title: {Title}");
                    break;
                case "ScriptType":
                    sw.Write($"ScriptType: {ScriptType}");
                    break;
                case "PlayResX":
                    sw.Write($"PlayResX: {PlayResX}");
                    break;
                case "PlayResY":
                    sw.Write($"PlayResY: {PlayResY}");
                    break;
                case "LayoutResX":
                    sw.Write($"LayoutResX: {LayoutResX}");
                    break;
                case "LayoutResY":
                    sw.Write($"LayoutResY: {LayoutResY}");
                    break;
                case "Timer":
                    sw.Write($"Timer: {Timer:0.000}");
                    break;
                case "WrapStyle":
                    sw.Write($"WrapStyle: {WrapStyle}");
                    break;
                case "ScaledBorderAndShadow":
                    sw.Write(string.Format("ScaledBorderAndShadow: {0}", ScaledBorderAndShadow ? "yes" : "no"));
                    break;
                case "Kerning":
                    sw.Write(string.Format("Kerning: {0}", Kerning ? "yes" : "no"));
                    break;
                case "YCbCr Matrix":
                    sw.Write($"YCbCr Matrix: {YCbCrMatrix.ToStringBuilder()}");
                    break;
                default:
                    if (Others.TryGetValue(k, out string? v))
                    {
                        sw.Write($"{k}: {v}");
                    }
                    break;
            }
            sw.Write(newline);
        }
        sw.Write(newline);
    }
}
