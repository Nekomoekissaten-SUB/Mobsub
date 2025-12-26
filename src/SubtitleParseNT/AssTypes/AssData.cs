using System.Text;
using Microsoft.Extensions.Logging;
using Mobsub.SubtitleParseNT2.AssUtils;
using ZLogger;

namespace Mobsub.SubtitleParseNT2.AssTypes;

public sealed class AssData(ILogger? logger = null, AssParseTarget target = AssParseTarget.Default)
{
    public bool CarriageReturn = true;
    private bool _getFirstCarriageReturn = false;
    public Encoding CharEncoding = Encoding.UTF8;
    public HashSet<AssSection> Sections = [];

    public AssScriptInfo ScriptInfo { get; set; } = new();
    public AssStyles Styles { get; set; } = new();
    public AssEvents? Events { get; set; }

    public IAssTagProcessor? Processor { get; private set; }
    internal Action<AssEvent>? EventViewAction { get; private set; }
    private bool eventInit = false;
    private void InitEvents()
    {
        switch (target)
        {
            case AssParseTarget.Default:
                break;
            case AssParseTarget.ParseAssFontsInfo:
                Processor = new AssFontProcessor(ScriptInfo.WrapStyle, Styles);
                EventViewAction = ev => Processor.Process(ev);
                break;
            case AssParseTarget.ParseAssFontsInfoWithEncoding:
                Processor = new AssFontProcessor(ScriptInfo.WrapStyle, Styles) { AnalyzeWithEncoding = true };
                EventViewAction = ev => Processor.Process(ev);
                break;
        }

        Events = new(logger) { OnEventParsed = EventViewAction };
        eventInit = true;
    }

    private byte[]? _sourceBuffer;

    public async Task<AssData> ReadAssFileAsync(Stream fs)
    {
        if (fs is MemoryStream ms && ms.TryGetBuffer(out ArraySegment<byte> segment))
        {
            _sourceBuffer = new byte[segment.Count];
            Buffer.BlockCopy(segment.Array!, segment.Offset, _sourceBuffer, 0, segment.Count);
        }
        else
        {
            using var memoryStream = new MemoryStream();
            await fs.CopyToAsync(memoryStream);
            _sourceBuffer = memoryStream.ToArray();
        }

        var span = _sourceBuffer.AsSpan();
        CharEncoding = Utils.GuessEncoding(span, out int preambleLength);
        CarriageReturn = false;
        
        logger?.ZLogInformation($"Start parse ass");
 
        var lineNumber = 0;
        var sectionType = AssSection.None;
        var offset = preambleLength;

        while (offset < span.Length)
        {
            // Find next line break
            int nextLineLength = 0;
            int nextOffset = offset;
            bool foundLine = false;

            // Simple search for \n or \r\n
            // Optimized search could use SIMD (IndexOfAny), but loop is sufficient for text
            int i = offset;
            while (i < span.Length)
            {
                if (span[i] == (byte)'\n')
                {
                    nextLineLength = i - offset; // Exclude \n
                    nextOffset = i + 1;
                    
                    // Handle \r\n (if prev char was \r)
                    if (nextLineLength > 0 && span[i - 1] == (byte)'\r')
                    {
                        nextLineLength--; // Exclude \r
                        if (!_getFirstCarriageReturn)
                        {
                            CarriageReturn = true;
                            _getFirstCarriageReturn = true;
                        }
                    }
                    foundLine = true;
                    break;
                }
                i++;
            }

            if (!foundLine)
            {
                // End of file without newline
                nextLineLength = span.Length - offset;
                nextOffset = span.Length;
            }

            lineNumber++;
            var lineSlice = new ReadOnlyMemory<byte>(_sourceBuffer, offset, nextLineLength);
            
            if (lineNumber == 1 && !lineSlice.Span.SequenceEqual("[Script Info]"u8))
            {
                 throw new Exception("Please check first line");
            }
            
            ParseContent(lineSlice, lineNumber, ref sectionType);
            
            offset = nextOffset;
        }

        logger?.ZLogInformation($"Ass parsing completed");
        return this;
    }
    public async Task<AssData> ReadAssFileAsync(string filePath)
    {
        using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read);
        logger?.ZLogInformation($"Open ass file: {filePath}");
        return await ReadAssFileAsync(fs);
    }
    public AssData ReadAssText(ReadOnlySpan<byte> data)
    {
        using var ms = new MemoryStream(data.ToArray(), writable: false);
        return ReadAssFileAsync(ms).GetAwaiter().GetResult();
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
                if (!eventInit)
                {
                    InitEvents();
                }
                Events!.Read(line, "[V4+ Styles]"u8, lineNumber);
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
