using Microsoft.Extensions.Logging;
using ZLogger;

namespace Mobsub.SubtitleParse.AssTypes;

public sealed class AssScriptInfo(ILogger? logger = null)
{
    private static readonly string[] ScriptTypes = ["v4.00", "v4.00+", "v4.00++"];
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
            if (!ScriptTypes.Contains(value, StringComparer.OrdinalIgnoreCase))
            {
                logger?.ZLogError($"ScriptType: {value} is invalid");
            }
            if (value.Length > 0 && value.AsSpan()[0] == 'V')
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
        var sp = Utils.TrimSpaces(line.Span);
        if (sp.IsEmpty) return;
        
        switch (sp[0])
        {
            case (byte)'!':
                CustomData.Add(Utils.GetString(line, Range.StartAt(1), true));
                logger?.ZLogDebug($"Line {lineNumber} is customized metadata");
                return;
            case (byte)';':
                Comment.Add(Utils.GetString(line, Range.StartAt(1), true));
                return;
        }

        if (sp.IndexOf((byte)':') is int idx && idx > 0)
        {
            var k = Utils.GetString(sp[..idx]);
            var valueSpan = Utils.TrimSpaces(sp[(idx + 1)..]);
            
            // Fast path for known integer properties to avoid string allocation for Value
            if (k.Equals(AssConstants.ScriptInfo.PlayResX, StringComparison.OrdinalIgnoreCase) && Utils.TryReadInt(ref valueSpan, out int prx))
            {
                PlayResX = prx;
            }
            else if (k.Equals(AssConstants.ScriptInfo.PlayResY, StringComparison.OrdinalIgnoreCase) && Utils.TryReadInt(ref valueSpan, out int pry))
            {
                PlayResY = pry;
            }
            else if (k.Equals(AssConstants.ScriptInfo.LayoutResX, StringComparison.OrdinalIgnoreCase) && Utils.TryReadInt(ref valueSpan, out int lrx))
            {
                LayoutResX = lrx;
            }
            else if (k.Equals(AssConstants.ScriptInfo.LayoutResY, StringComparison.OrdinalIgnoreCase) && Utils.TryReadInt(ref valueSpan, out int lry))
            {
                LayoutResY = lry;
            }
            else if (k.Equals(AssConstants.ScriptInfo.Timer, StringComparison.OrdinalIgnoreCase) && Utils.TryReadDouble(ref valueSpan, out double t))
            {
                Timer = (float)t;
            }
            else if (k.Equals(AssConstants.ScriptInfo.WrapStyle, StringComparison.OrdinalIgnoreCase) && Utils.TryReadInt(ref valueSpan, out int ws))
            {
                WrapStyle = (byte)ws;
            }
            else if (k.Equals(AssConstants.ScriptInfo.ScaledBorderAndShadow, StringComparison.OrdinalIgnoreCase))
            {
                ScaledBorderAndShadow = valueSpan.SequenceEqual("yes"u8);
            }
             else if (k.Equals(AssConstants.ScriptInfo.Kerning, StringComparison.OrdinalIgnoreCase))
            {
                Kerning = valueSpan.SequenceEqual("yes"u8);
            }
            else if (k.Equals(AssConstants.ScriptInfo.YCbCrMatrix, StringComparison.OrdinalIgnoreCase))
            {
                int dotIdx = valueSpan.IndexOf((byte)'.');
                if (dotIdx < 0)
                {
                    YCbCrMatrix.Matrix = Utils.GetString(valueSpan);
                }
                else
                {
                    YCbCrMatrix.Full = !valueSpan[..dotIdx].SequenceEqual("TV"u8);
                    YCbCrMatrix.Matrix = Utils.GetString(valueSpan, Range.StartAt(dotIdx + 1));
                }
            }
            else if (k.Equals(AssConstants.ScriptInfo.ScriptType, StringComparison.OrdinalIgnoreCase))
            {
                ScriptType = Utils.GetString(valueSpan);
            }
             else if (k.Equals(AssConstants.ScriptInfo.Title, StringComparison.OrdinalIgnoreCase))
            {
                Title = Utils.GetString(valueSpan);
            }
            else if (k.Equals(AssConstants.ScriptInfo.OriginalScript, StringComparison.OrdinalIgnoreCase))
            {
                OriginalScript = Utils.GetString(valueSpan);
            }
            else if (k.Equals(AssConstants.ScriptInfo.OriginalTranslation, StringComparison.OrdinalIgnoreCase))
            {
                OriginalTranslation = Utils.GetString(valueSpan);
            }
            else if (k.Equals(AssConstants.ScriptInfo.OriginalEditing, StringComparison.OrdinalIgnoreCase))
            {
                OriginalEditing = Utils.GetString(valueSpan);
            }
            else if (k.Equals(AssConstants.ScriptInfo.OriginalTiming, StringComparison.OrdinalIgnoreCase))
            {
                OriginalTiming = Utils.GetString(valueSpan);
            }
             else if (k.Equals(AssConstants.ScriptInfo.ScriptUpdatedBy, StringComparison.OrdinalIgnoreCase))
            {
                ScriptUpdatedBy = Utils.GetString(valueSpan);
            }
            else if (k.Equals(AssConstants.ScriptInfo.UpdateDetails, StringComparison.OrdinalIgnoreCase))
            {
                UpdateDetails = Utils.GetString(valueSpan);
            }
            else
            {
                 // Unknown key
                 Others[k] = Utils.GetString(valueSpan);
            }

            if (!Orders.Add(k))
            {
                logger?.ZLogWarning($"Duplicate key in Script Info: {k}");
            }
        }
        else
        {
             logger?.ZLogError($"Unknown line #{lineNumber}: '{Utils.GetString(line)}'");
        }
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

    public AssScriptInfo DeepClone()
    {
        return new AssScriptInfo()
        {
            ScriptType = ScriptType,
            PlayResX = PlayResX,
            PlayResY = PlayResY,
            LayoutResX = LayoutResX,
            LayoutResY = LayoutResY,
            Timer = Timer,
            WrapStyle = WrapStyle,
            ScaledBorderAndShadow = ScaledBorderAndShadow,
            Kerning = Kerning,
            YCbCrMatrix = (AssYCbCrMatrix)YCbCrMatrix.Clone(),
            Title = Title,
            OriginalScript = OriginalScript,
            OriginalTranslation = OriginalTranslation,
            OriginalEditing = OriginalEditing,
            OriginalTiming = OriginalTiming,
            ScriptUpdatedBy = ScriptUpdatedBy,
            UpdateDetails = UpdateDetails,
            Comment = new List<string>(Comment),
            CustomData = new List<string>(CustomData),
            Others = new Dictionary<string, string>(Others),
            Orders = new HashSet<string>(Orders)
        };
    }
}
