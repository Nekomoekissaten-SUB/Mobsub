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
    public AssEmbeddedSection Fonts { get; set; } = new(AssEmbeddedFileType.Font);
    public AssEmbeddedSection Graphics { get; set; } = new(AssEmbeddedFileType.Graphics);
    public AssMetaData ProjectGarbage { get; set; } = new();
    public AssMetaData Extradata { get; set; } = new();

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
    private string? _scriptTypeCache;
    private byte[]? _scriptTypeBytes;

    public async Task<AssData> ReadAssFileAsync(Stream fs)
        => await ReadAssFileCoreAsync(fs, forcedEncoding: null, detector: null, fallbackEncoding: null);

    public async Task<AssData> ReadAssFileAsync(Stream fs, Encoding encoding)
        => await ReadAssFileCoreAsync(fs, encoding, detector: null, fallbackEncoding: null);

    public async Task<AssData> ReadAssFileAsync(Stream fs, Func<ReadOnlySpan<byte>, Encoding?> detector, Encoding? fallbackEncoding = null)
        => await ReadAssFileCoreAsync(fs, forcedEncoding: null, detector, fallbackEncoding);

    private async Task<AssData> ReadAssFileCoreAsync(Stream fs, Encoding? forcedEncoding, Func<ReadOnlySpan<byte>, Encoding?>? detector, Encoding? fallbackEncoding)
    {
        _getFirstCarriageReturn = false;
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

        return ParseBuffer(_sourceBuffer, forcedEncoding, detector, fallbackEncoding);
    }
    public async Task<AssData> ReadAssFileAsync(string filePath)
    {
        using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read);
        logger?.ZLogInformation($"Open ass file: {filePath}");
        return await ReadAssFileAsync(fs);
    }
    public async Task<AssData> ReadAssFileAsync(string filePath, Encoding encoding)
    {
        using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read);
        logger?.ZLogInformation($"Open ass file: {filePath}");
        return await ReadAssFileAsync(fs, encoding);
    }
    public async Task<AssData> ReadAssFileAsync(string filePath, Func<ReadOnlySpan<byte>, Encoding?> detector, Encoding? fallbackEncoding = null)
    {
        using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read);
        logger?.ZLogInformation($"Open ass file: {filePath}");
        return await ReadAssFileAsync(fs, detector, fallbackEncoding);
    }
    public AssData ReadAssText(ReadOnlySpan<byte> data)
    {
        _getFirstCarriageReturn = false;
        var buffer = data.ToArray();
        return ParseBuffer(buffer, forcedEncoding: null, detector: null, fallbackEncoding: null);
    }
    public AssData ReadAssText(ReadOnlySpan<byte> data, Encoding encoding)
    {
        _getFirstCarriageReturn = false;
        var buffer = data.ToArray();
        return ParseBuffer(buffer, encoding, detector: null, fallbackEncoding: null);
    }
    public AssData ReadAssText(ReadOnlySpan<byte> data, Func<ReadOnlySpan<byte>, Encoding?> detector, Encoding? fallbackEncoding = null)
    {
        _getFirstCarriageReturn = false;
        var buffer = data.ToArray();
        return ParseBuffer(buffer, forcedEncoding: null, detector, fallbackEncoding);
    }

    private AssData ParseBuffer(byte[] buffer, Encoding? forcedEncoding, Func<ReadOnlySpan<byte>, Encoding?>? detector, Encoding? fallbackEncoding)
    {
        var previousLogger = AssEventParser.Logger;
        AssEventParser.Logger = logger;
        try
        {
            _sourceBuffer = buffer;
            _scriptTypeCache = null;
            _scriptTypeBytes = null;
            var span = _sourceBuffer.AsSpan();
            int preambleLength;

            if (forcedEncoding != null)
            {
                CharEncoding = forcedEncoding;
                preambleLength = GetPreambleLength(span, forcedEncoding);
            }
            else
            {
                CharEncoding = Utils.GuessEncoding(span, out preambleLength);
                if (preambleLength == 0 && detector != null)
                {
                    var detected = detector(span);
                    if (detected != null)
                    {
                        CharEncoding = detected;
                        preambleLength = GetPreambleLength(span, detected);
                    }
                }

                if (preambleLength == 0 && fallbackEncoding != null && CharEncoding.CodePage == Encoding.UTF8.CodePage)
                {
                    CharEncoding = fallbackEncoding;
                }
            }

            CarriageReturn = false;
            if (CharEncoding.CodePage != Encoding.UTF8.CodePage)
            {
                var decoded = CharEncoding.GetString(span.Slice(preambleLength));
                _sourceBuffer = Encoding.UTF8.GetBytes(decoded);
                span = _sourceBuffer.AsSpan();
                preambleLength = 0;
            }

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

            Fonts.Finish();
            Graphics.Finish();

            logger?.ZLogInformation($"Ass parsing completed");
            return this;
        }
        finally
        {
            AssEventParser.Logger = previousLogger;
        }
    }

    private static int GetPreambleLength(ReadOnlySpan<byte> span, Encoding encoding)
    {
        var preamble = encoding.GetPreamble();
        if (preamble.Length == 0 || span.Length < preamble.Length)
            return 0;

        return span.Slice(0, preamble.Length).SequenceEqual(preamble) ? preamble.Length : 0;
    }

    private ReadOnlySpan<byte> GetScriptTypeBytes()
    {
        var scriptType = ScriptInfo.ScriptType;
        if (_scriptTypeBytes == null || !string.Equals(_scriptTypeCache, scriptType, StringComparison.Ordinal))
        {
            _scriptTypeCache = scriptType;
            _scriptTypeBytes = Encoding.UTF8.GetBytes(scriptType);
        }
        return _scriptTypeBytes;
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
            else if (sp.SequenceEqual("[Fonts]"u8))
            {
                sectionType = AssSection.Fonts;
            }
            else if (sp.SequenceEqual("[Graphics]"u8))
            {
                sectionType = AssSection.Graphics;
            }
            else if (sp.SequenceEqual("[Aegisub Project Garbage]"u8))
            {
                sectionType = AssSection.AegisubProjectGarbage;
            }
            else if (sp.SequenceEqual("[Aegisub Extradata]"u8))
            {
                sectionType = AssSection.AegisubExtradata;
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
                Events!.Read(line, GetScriptTypeBytes(), lineNumber);
                break;
            case AssSection.AegisubProjectGarbage:
                ProjectGarbage.Read(line, lineNumber);
                break;
            case AssSection.AegisubExtradata:
                Extradata.Read(line, lineNumber);
                break;
            case AssSection.Fonts:
                Fonts.Read(line, lineNumber);
                break;
            case AssSection.Graphics:
                Graphics.Read(line, lineNumber);
                break;
            default:
                break;
        }
    }
}
