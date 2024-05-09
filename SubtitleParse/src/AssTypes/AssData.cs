using System.Text;
using Microsoft.Extensions.Logging;
using ZLogger;

namespace Mobsub.SubtitleParse.AssTypes;

public class AssData(ILogger? logger = null)
{
    public bool CarriageReturn = false;
    public Encoding CharEncoding = Utils.EncodingRefOS();
    public HashSet<AssSection> Sections = [];
    public AssScriptInfo ScriptInfo {get; set;} = new AssScriptInfo(logger){};
    public AssStyles Styles { get; set; } = new AssStyles(logger) {};
    public AssEvents Events {get; set;} = new AssEvents(logger) {};
    public Dictionary<string, string?> AegisubProjectGarbage =  [];
    public List<string> AegiusbExtradata = [];
    public List<AssEmbedded.Font> Fonts = [];
    public List<AssEmbedded.Graphic> Graphics = [];

    private readonly ILogger? _logger = logger;
    private const string sectionNameFonts = "[Fonts]";
    private const string sectionNameGraphics = "[Graphics]";
    private const string sectionNameAegisubProjectGarbage = "[Aegisub Project Garbage]";
    private const string sectionNameAegisubExtradata = "[Aegisub Extradata]";

    public AssData ReadAssFile(FileStream fs)
    {
        using var sr = new StreamReader(fs);
        string? line;
        var lineNumber = 0;
        var parseFunc = AssSection.ScriptInfo;
        Utils.GuessEncoding(fs, out CharEncoding, out CarriageReturn);
        _logger?.ZLogInformation($"File use {CharEncoding.EncodingName} and {(CarriageReturn ? "CRLF" : "LF")}");
        _logger?.ZLogInformation($"Start parse ass");

        while ((line = sr.ReadLine()) != null)
        {
            lineNumber++;
            var sp = line.AsSpan();

            if (lineNumber == 1 && !sp.SequenceEqual("[Script Info]".AsSpan()))
            {
                throw new Exception("Please check first line");
            }

            if (sp.Length == 0)
            {
                continue;
            }

            if (sp[0] == '[')
            {
                _logger?.ZLogInformation($"Start parse section {line}");
                parseFunc = line switch
                {
                    AssScriptInfo.sectionName => AssSection.ScriptInfo,
                    AssStyles.sectionNameV4 => AssSection.StylesV4,
                    AssStyles.sectionNameV4P => AssSection.StylesV4P,
                    AssStyles.sectionNameV4PP => AssSection.StylesV4PP,
                    AssEvents.sectionName => AssSection.Events,
                    sectionNameFonts => AssSection.Fonts,
                    sectionNameGraphics => AssSection.Graphics,
                    sectionNameAegisubProjectGarbage => AssSection.AegisubProjectGarbage,
                    sectionNameAegisubExtradata => AssSection.AegisubExtradata,
                    _ => throw new Exception($"Unknown section: {line}."),
                };

                if (!Sections.Add(parseFunc))
                {
                    throw new Exception($"Duplicate section: {line}");
                }
                continue;
            }
            
            switch (parseFunc)
            {
                case AssSection.ScriptInfo:
                    ScriptInfo.Read(sp, lineNumber);
                    break;
                case AssSection.StylesV4:
                case AssSection.StylesV4P:
                case AssSection.StylesV4PP:
                    Styles.Read(sp, lineNumber);
                    break;
                case AssSection.Events:
                    Events.Read(sp, ScriptInfo.ScriptType, lineNumber);
                    break;
                case AssSection.AegisubProjectGarbage:
                    Utils.TrySplitKeyValue(sp, out var k, out var v);
                    AegisubProjectGarbage.TryAdd(k, v);
                    break;
                case AssSection.AegisubExtradata:
                    Utils.TrySplitKeyValue(sp, out var k1, out var v1);
                    AegiusbExtradata.Add(v1);
                    break;
                case AssSection.Fonts:
                    Fonts = AssEmbedded.ParseFontsFromAss(sp, lineNumber, _logger);
                    break;
                case AssSection.Graphics:
                    Graphics = AssEmbedded.ParseGraphicsFromAss(sp, lineNumber, _logger);
                    break;
                default:
                    break;
            }
        }
        _logger?.ZLogInformation($"Ass parsing completed");
        return this;
    }
    public AssData ReadAssFile(string filePath)
    {
        using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read);
        _logger?.ZLogInformation($"Open ass file: {filePath}");
        return ReadAssFile(fs);
    }

    public void WriteAssFile(string filePath, bool forceEnv, bool ctsRounding)
    {
        _logger?.ZLogInformation($"File will write to {filePath}");
        var newline = forceEnv ? [.. Environment.NewLine] : (CarriageReturn ? new char[] { '\r', '\n' } : ['\n']);
        var charEncoding = forceEnv ? Utils.EncodingRefOS() : CharEncoding;

        using FileStream fileStream = new(filePath, FileMode.Create, FileAccess.Write);
        using var memStream = new MemoryStream();
        using var sw = new StreamWriter(memStream, charEncoding);
        foreach (var s in Sections)
        {
            switch (s)
            {
                case AssSection.ScriptInfo:
                    ScriptInfo.Write(sw, newline);
                    break;
                case AssSection.StylesV4P:
                case AssSection.StylesV4:
                    Styles.Write(sw, newline, ScriptInfo.ScriptType);
                    break;
                case AssSection.Events:
                    Events.Write(sw, newline, ctsRounding);
                    break;
                case AssSection.Fonts:
                    sw.Write(sectionNameFonts);
                    sw.Write(newline);
                    foreach (var o in Fonts)
                    {
                        o.Write(sw, newline);
                    }
                    break;
                case AssSection.Graphics:
                    sw.Write(sectionNameGraphics);
                    sw.Write(newline);
                    foreach (var o in Fonts)
                    {
                        o.Write(sw, newline);
                    }
                    break;
                case AssSection.AegisubProjectGarbage:
                    sw.Write(sectionNameAegisubProjectGarbage);
                    sw.Write(newline);
                    foreach (var kvp in AegisubProjectGarbage)
                    {
                        sw.Write($"{kvp.Key}: {kvp.Value}");
                        sw.Write(newline);
                    }
                    sw.Write(newline);
                    break;
                case AssSection.AegisubExtradata:
                    sw.Write(sectionNameAegisubExtradata);
                    sw.Write(newline);
                    for (var i = 0; i < AegiusbExtradata.Count; i++)
                    {
                        sw.Write(AegiusbExtradata.ToArray()[i]);
                        sw.Write(newline);
                    }
                    sw.Write(newline);
                    break;
            }
        }
        sw.Flush();
        _logger?.ZLogInformation($"Sections write completed");

        memStream.Seek(0, SeekOrigin.Begin);
        memStream.CopyTo(fileStream);
        fileStream.Close();
        _logger?.ZLogInformation($"File write completed");
    }
    public void WriteAssFile(string filePath) => WriteAssFile(filePath, false, false);

}
