using Microsoft.Extensions.Logging;
using System.Text;
using ZLogger;
using Cysharp.IO;
using System.Threading.Tasks;

namespace Mobsub.SubtitleParseNT2.AssTypes;

public sealed class AssData(ILogger? logger = null)
{
    public bool CarriageReturn = true;
    public Encoding CharEncoding = Utils.EncodingRefOS();
    public HashSet<AssSection> Sections = [];

    public AssScriptInfo ScriptInfo { get; set; } = new(logger);
    public AssStyles Styles { get; set; } = new(logger);
    public AssEvents Events { get; set; } = new(logger);

    public async Task<AssData> ReadAssFile(FileStream fs)
    {
        using var sr = new Utf8StreamReader(fs);
        var lineNumber = 0;
        var sectionType = AssSection.None;
        Utils.GuessEncoding(fs, out CharEncoding, out CarriageReturn);
        logger?.ZLogInformation($"File use {CharEncoding.EncodingName} and {(CarriageReturn ? "CRLF" : "LF")}");
        logger?.ZLogInformation($"Start parse ass");

        ReadOnlyMemory<byte>? line;
        while ((line = await sr.ReadLineAsync()) != null)
        {
            lineNumber++;
            if (lineNumber == 1 && !((ReadOnlyMemory<byte>)line).Span.SequenceEqual("[Script Info]"u8))
            {
                throw new Exception("Please check first line");
            }
            ParseContent((ReadOnlyMemory<byte>)line, lineNumber, ref sectionType);
        }

        logger?.ZLogInformation($"Ass parsing completed");
        return this;
    }
    public async Task<AssData> ReadAssFile(string filePath)
    {
        using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read);
        logger?.ZLogInformation($"Open ass file: {filePath}");
        return await ReadAssFile(fs);
    }

    private void ParseContent(ReadOnlyMemory<byte> line, int lineNumber, ref AssSection sectionType)
    {
        var sp = line.Span;
        if (sp.Length == 0)
        {
            return;
        }

        if (sp[0] == '[')
        {
            logger?.ZLogInformation($"Start parse section {sp.ToString()}");
            if (sp.SequenceEqual("[Script Info]"u8))
            {
                sectionType = AssSection.ScriptInfo;
            }
            else if (sp.SequenceEqual("[V4+ Styles]"u8))
            {
                sectionType = AssSection.StylesV4P;
            }
            else if (sp.SequenceEqual("[Events]"u8))
            {
                sectionType = AssSection.Events;
            }
            else
            {
                throw new Exception($"Unknown section: {sp.ToString()}.");
            }

            if (!Sections.Add(sectionType))
            {
                throw new Exception($"Duplicate section: {sp.ToString()}");
            }
            return;
        }

        switch (sectionType)
        {
            case AssSection.ScriptInfo:
                ScriptInfo.Read(line, lineNumber);
                break;
            case AssSection.StylesV4:
            case AssSection.StylesV4P:
            case AssSection.StylesV4PP:
                Styles.Read(line, lineNumber);
                break;
            case AssSection.Events:
                Events.Read(line, "[V4+ Styles]"u8, lineNumber);
                break;
            //case AssSection.AegisubProjectGarbage:
            //    Utils.TrySplitKeyValue(sp, out var k, out var v);
            //    AegisubProjectGarbage.TryAdd(k, v);
            //    break;
            //case AssSection.AegisubExtradata:
            //    Utils.TrySplitKeyValue(sp, out var k1, out var v1);
            //    AegiusbExtradata.Add(k1 == string.Empty ? sp.ToString() : v1);
            //    break;
            //case AssSection.Fonts:
            //    Fonts = AssEmbedded.ParseFontsFromAss(sp, lineNumber, _logger);
            //    break;
            //case AssSection.Graphics:
            //    Graphics = AssEmbedded.ParseGraphicsFromAss(sp, lineNumber, _logger);
            //    break;
            default:
                break;
        }
    }
}
