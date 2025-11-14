using Microsoft.Extensions.Logging;
using ZLogger;

namespace Mobsub.SubtitleParseNT2.AssTypes;

public sealed class AssScriptInfo(ILogger? logger = null)
{
    private readonly string[] scriptTypes = ["v4.00", "v4.00+", "v4.00++"];
    private string scriptType = "v4.00";
    private int? layoutResX = null;
    private int? layoutResY = null;
    private float timer = 100.0000f;
    private byte? wrapStyle = null;
    
    public string ScriptType
    {
        get => scriptType;
        set
        {
            if (!scriptTypes.Contains(value, StringComparer.OrdinalIgnoreCase))
            {
                logger?.ZLogError($"ScriptType: {value} is invalid");
            }
            if (value.AsSpan()[0] == 'V')
            {
                logger?.ZLogWarning($"ScriptType is {value}, it should be start with 'v'");
            }
            scriptType = value;
        }
    }
    public int PlayResX {get; set;} = 640;
    public int PlayResY {get; set;} = 480;
    public int LayoutResX
    {
        get => layoutResX ?? PlayResX;
        set => layoutResX = value;
    }
    public int LayoutResY
    {
        get => layoutResY ?? PlayResY;
        set => layoutResY = value;
    }
    public float Timer
    {
        get => timer;
        set => timer = (float)Math.Round(value, 4);
    }
    public byte WrapStyle
    {
        get => wrapStyle ?? 0;
        set
        {
            if (scriptType.Equals("v4.00", StringComparison.OrdinalIgnoreCase))
            {
                logger?.ZLogError($"WrapStyle: ass / ssa version {scriptType} don’t support {value}");
            }
            wrapStyle = value;
        }
    }
    public bool ScaledBorderAndShadow {get; set;} = true; // yes or no
    public bool Kerning {get; set;} = false;    // yes or no, only libass, vsfilter is disabled
    public AssYCbCrMatrix YCbCrMatrix {get; set;} = new AssYCbCrMatrix();
    
    public string? Title { get; set; }
    public string? OriginalScript { get; set; }
    public string? OriginalTranslation { get; set; }
    public string? OriginalEditing { get; set; }
    public string? OriginalTiming { get; set; }
    public string? ScriptUpdatedBy { get; set; }
    public string? UpdateDetails { get; set; }

    public List<string> Comment = [];
    public List<string> CustomData = [];
    public Dictionary<string, string> Others = [];
    // public int status = 0;
    public HashSet<string> Orders = [];


    public void Read(ReadOnlyMemory<byte> line, int lineNumber)
    {
        var sp = line.Span;
        switch (sp[0])
        {
            case (byte)'!':
                CustomData.Add(Utils.GetString(line, Range.StartAt(1), true));
                logger?.ZLogDebug($"Line {lineNumber} is customized metadata");
                return;
            case (byte)';':
                Comment.Add(Utils.GetString(line, Range.StartAt(1), true));
                return;
            default:
                break;
        }

        var idx = sp.IndexOf((byte)':');
        if (idx < 0)
        {
            logger?.ZLogError($"Unknown line #{lineNumber}: '{Utils.GetString(line)}'");
            return;
        }
            
        var k = Utils.GetString(sp[..idx]);
        var v = Utils.TrimSpaces(sp[(idx + 1)..]);
        if (Utils.IsStringInFields(new AssConstants.ScriptInfo(), typeof(AssConstants.ScriptInfo), k))
        {
            if (k.SequenceEqual(AssConstants.ScriptInfo.YCbCrMatrix))
            {
                idx = v.IndexOf((byte)'.');
                if (idx < 0)
                {
                    YCbCrMatrix.Matrix = Utils.GetString(v);
                }
                else
                {
                    YCbCrMatrix.Full = !v[..idx].SequenceEqual("TV"u8);
                    YCbCrMatrix.Matrix = Utils.GetString(v, Range.StartAt(idx + 1));
                }
            }
            else
            {
                Utils.SetProperty(this, typeof(AssScriptInfo), k.Contains(' ') ? k.Replace(" ", "") : k, v);
            }
        }
        else
        {
            Others[k] = Utils.GetString(v);
        }

        if (!Orders.Add(k))
        {
            logger?.ZLogError($"Duplicate key in Script Info: {k}");
        }
        logger?.ZLogDebug($"Line {lineNumber} is a key-pair, key {k} parse completed");
    }

    public void Write(StreamWriter sw, char[] newline)
    {
        logger?.ZLogInformation($"Start write section {AssConstants.SectionScriptInfo}");
        sw.Write(AssConstants.SectionScriptInfo);
        sw.Write(newline);

        foreach (var s in Comment)
        {
            sw.Write($"; {s}");
            sw.Write(newline);
        }
        logger?.ZLogDebug($"Write comment lines fine");

        foreach (var k in Orders)
        {
            switch (k)
            {
                case AssConstants.ScriptInfo.ScriptType:
                    sw.Write($"{k}: {ScriptType}");
                    break;
                case AssConstants.ScriptInfo.PlayResX:
                    sw.Write($"{k}: {PlayResX}");
                    break;
                case AssConstants.ScriptInfo.PlayResY:
                    sw.Write($"{k}: {PlayResY}");
                    break;
                case AssConstants.ScriptInfo.LayoutResX:
                    sw.Write($"{k}: {LayoutResX}");
                    break;
                case AssConstants.ScriptInfo.LayoutResY:
                    sw.Write($"{k}: {LayoutResY}");
                    break;
                case AssConstants.ScriptInfo.Timer:
                    sw.Write($"{k}: {Timer:0.000}");
                    break;
                case AssConstants.ScriptInfo.WrapStyle:
                    sw.Write($"{k}: {WrapStyle}");
                    break;
                case AssConstants.ScriptInfo.ScaledBorderAndShadow:
                    sw.Write($"{k}: {(ScaledBorderAndShadow ? "yes" : "no")}");
                    break;
                case AssConstants.ScriptInfo.Kerning:
                    sw.Write($"{k}: {(Kerning ? "yes" : "no")}");
                    break;
                case AssConstants.ScriptInfo.YCbCrMatrix:
                    sw.Write($"{k}: {YCbCrMatrix.ToStringBuilder()}");
                    break;

                case AssConstants.ScriptInfo.Title:
                    sw.Write($"Title: {Title}");
                    break;
                case AssConstants.ScriptInfo.OriginalScript:
                    sw.Write($"{k}: {OriginalScript}");
                    break;
                case AssConstants.ScriptInfo.OriginalTranslation:
                    sw.Write($"{k}: {OriginalTranslation}");
                    break;
                case AssConstants.ScriptInfo.OriginalEditing:
                    sw.Write($"{k}: {OriginalEditing}");
                    break;
                case AssConstants.ScriptInfo.OriginalTiming:
                    sw.Write($"{k}: {OriginalTiming}");
                    break;
                case AssConstants.ScriptInfo.ScriptUpdatedBy:
                    sw.Write($"{k}: {ScriptUpdatedBy}");
                    break;
                case AssConstants.ScriptInfo.UpdateDetails:
                    sw.Write($"{k}: {UpdateDetails}");
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
        logger?.ZLogDebug($"Write key-pair lines fine");

        foreach (var s in CustomData)
        {
            sw.Write($"!: {s}");
            sw.Write(newline);
        }
        logger?.ZLogDebug($"Write customized metadata lines fine");

        //sw.Write(newline);
        logger?.ZLogDebug($"Section write completed");
    }
}
