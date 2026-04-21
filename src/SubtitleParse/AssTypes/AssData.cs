﻿using System.Text;
using Microsoft.Extensions.Logging;
using Mobsub.SubtitleParse.AssText;
using Mobsub.SubtitleParse.AssUtils;
using ZLogger;

namespace Mobsub.SubtitleParse.AssTypes;

public sealed class AssData(ILogger? logger = null, AssParseTarget target = AssParseTarget.Default)
{
    public bool CarriageReturn = true;
    public bool AllowUnknownSections = true;
    public bool ExtractAegisubExtradataMarkers = false;
    public bool PreserveAegisubExtradataMarkersOnWrite = true;
    private bool _getFirstCarriageReturn = false;
    public Encoding CharEncoding = Encoding.UTF8;
    public HashSet<AssSection> Sections = [];

    public AssScriptInfo ScriptInfo { get; set; } = new();
    public AssStyles Styles { get; set; } = new();
    public AssEvents? Events { get; set; }
    public AssEmbeddedSection Fonts { get; set; } = new(AssEmbeddedFileType.Font);
    public AssEmbeddedSection Graphics { get; set; } = new(AssEmbeddedFileType.Graphics);
    public AssMetaData ProjectGarbage { get; set; } = new();
    public AssAegisubExtradata Extradata { get; set; } = new();

    public IAssEventTextProcessor? Processor { get; private set; }
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

        Events = new(logger)
        {
            OnEventParsed = EventViewAction,
            ExtractAegisubExtradataMarkers = ExtractAegisubExtradataMarkers,
            PreserveAegisubExtradataMarkersOnWrite = PreserveAegisubExtradataMarkersOnWrite
        };
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

    public AssData ReadAssFile(Stream fs)
        => ReadAssFileAsync(fs).GetAwaiter().GetResult();

    public AssData ReadAssFile(Stream fs, Encoding encoding)
        => ReadAssFileAsync(fs, encoding).GetAwaiter().GetResult();

    public AssData ReadAssFile(Stream fs, Func<ReadOnlySpan<byte>, Encoding?> detector, Encoding? fallbackEncoding = null)
        => ReadAssFileAsync(fs, detector, fallbackEncoding).GetAwaiter().GetResult();

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

    public AssData ReadAssFile(string filePath)
        => ReadAssFileAsync(filePath).GetAwaiter().GetResult();

    public AssData ReadAssFile(string filePath, Encoding encoding)
        => ReadAssFileAsync(filePath, encoding).GetAwaiter().GetResult();

    public AssData ReadAssFile(string filePath, Func<ReadOnlySpan<byte>, Encoding?> detector, Encoding? fallbackEncoding = null)
        => ReadAssFileAsync(filePath, detector, fallbackEncoding).GetAwaiter().GetResult();
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

    /// <summary>
    /// Parse ASS from an existing byte[] without copying.
    /// The input buffer is retained for the lifetime of this AssData instance.
    /// </summary>
    public AssData ReadAssBytes(byte[] data, Encoding encoding)
    {
        _getFirstCarriageReturn = false;
        return ParseBuffer(data, encoding, detector: null, fallbackEncoding: null);
    }
    public AssData ReadAssText(ReadOnlySpan<byte> data, Func<ReadOnlySpan<byte>, Encoding?> detector, Encoding? fallbackEncoding = null)
    {
        _getFirstCarriageReturn = false;
        var buffer = data.ToArray();
        return ParseBuffer(buffer, forcedEncoding: null, detector, fallbackEncoding);
    }

    public void WriteAssFile(string filePath, bool forceEnv = false, bool ctsRounding = false)
    {
        logger?.ZLogInformation($"File will write to {filePath}");
        var charEncoding = forceEnv ? Utils.EncodingRefOS() : CharEncoding;

        using FileStream fileStream = new(filePath, FileMode.Create, FileAccess.Write);
        using var memStream = new MemoryStream();
        using var sw = new StreamWriter(memStream, charEncoding);
        WriteAssStream(sw, forceEnv, ctsRounding);

        memStream.Seek(0, SeekOrigin.Begin);
        memStream.CopyTo(fileStream);
        fileStream.Close();
        logger?.ZLogInformation($"File write completed");
    }

    public void WriteAssFile(string filePath) => WriteAssFile(filePath, forceEnv: false, ctsRounding: false);

    private void WriteAssStream(StreamWriter sw, bool forceEnv, bool ctsRounding)
    {
        logger?.ZLogInformation($"Begin write ass");
        var newline = forceEnv ? [.. Environment.NewLine] : (CarriageReturn ? new[] { '\r', '\n' } : ['\n']);

        foreach (var s in Sections)
        {
            switch (s)
            {
                case AssSection.ScriptInfo:
                    ScriptInfo.Write(sw, newline);
                    break;
                case AssSection.StylesV4P:
                case AssSection.StylesV4:
                case AssSection.StylesV4PP:
                    sw.Write(newline);
                    Styles.Write(sw, newline, s);
                    break;
                case AssSection.Events:
                    if (Events == null)
                        break;
                    sw.Write(newline);
                    Events.Write(sw, newline, ctsRounding);
                    break;
                case AssSection.Fonts:
                    sw.Write(newline);
                    WriteEmbeddedSection(sw, AssConstants.SectionFonts, AssConstants.EmbeddedSectionHeaderKeys.FontName, Fonts, newline);
                    break;
                case AssSection.Graphics:
                    sw.Write(newline);
                    WriteEmbeddedSection(sw, AssConstants.SectionGraphics, AssConstants.EmbeddedSectionHeaderKeys.FileName, Graphics, newline);
                    break;
                case AssSection.AegisubProjectGarbage:
                    sw.Write(newline);
                    WriteMetaSection(sw, AssConstants.SectionAegisubProjectGarbage, ProjectGarbage, newline);
                    break;
                case AssSection.AegisubExtradata:
                    sw.Write(newline);
                    Extradata.WriteSection(sw, newline);
                    break;
                default:
                    break;
            }
        }
        sw.Flush();
        logger?.ZLogInformation($"Sections write completed");
    }

    private static void WriteEmbeddedSection(StreamWriter sw, string sectionName, string headerKey, AssEmbeddedSection section, char[] newline)
    {
        sw.Write(sectionName);
        sw.Write(newline);
        foreach (var file in section.Files)
        {
            sw.Write(headerKey);
            sw.Write(": ");
            sw.Write(string.IsNullOrEmpty(file.OriginalName) ? file.Name : file.OriginalName);
            sw.Write(newline);

            foreach (var line in file.Data)
            {
                sw.Write(Utils.GetString(line));
                sw.Write(newline);
            }
        }
    }

    private static void WriteMetaSection(StreamWriter sw, string sectionName, AssMetaData data, char[] newline)
    {
        sw.Write(sectionName);
        sw.Write(newline);
        foreach (var kvp in data.Data)
        {
            if (string.IsNullOrEmpty(kvp.Value))
            {
                sw.Write(kvp.Key);
            }
            else
            {
                sw.Write($"{kvp.Key}: {kvp.Value}");
            }
            sw.Write(newline);
        }
    }

    public void Like(AssData ass)
    {
        CarriageReturn = ass.CarriageReturn;
        CharEncoding = ass.CharEncoding;
        ScriptInfo = ass.ScriptInfo.DeepClone();
        Events = new AssEvents();
        Sections =
        [
            AssSection.ScriptInfo,
            ass.Sections.Contains(AssSection.StylesV4P) ? AssSection.StylesV4P
                : ass.Sections.Contains(AssSection.StylesV4) ? AssSection.StylesV4
                : AssSection.StylesV4PP,
            AssSection.Events
        ];
    }

    private AssData ParseBuffer(byte[] buffer, Encoding? forcedEncoding, Func<ReadOnlySpan<byte>, Encoding?>? detector, Encoding? fallbackEncoding)
    {
        var previousLogger = AssEventTextParser.Logger;
        AssEventTextParser.Logger = logger;
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
            // Find next line break.
            int nextLineLength;
            int nextOffset;

            var searchSpan = span[offset..];
            int lfIndex = searchSpan.IndexOf((byte)'\n');
            if (lfIndex == -1)
            {
                // End of file without newline
                nextLineLength = span.Length - offset;
                nextOffset = span.Length;
            }
            else
            {
                nextLineLength = lfIndex; // Exclude \n
                nextOffset = offset + lfIndex + 1;

                // Handle \r\n (if prev char was \r)
                if (lfIndex > 0 && searchSpan[lfIndex - 1] == (byte)'\r')
                {
                    nextLineLength--; // Exclude \r
                    if (!_getFirstCarriageReturn)
                    {
                        CarriageReturn = true;
                        _getFirstCarriageReturn = true;
                    }
                }
            }

                lineNumber++;
                var lineSlice = new ReadOnlyMemory<byte>(_sourceBuffer, offset, nextLineLength);

                if (lineNumber == 1 && !lineSlice.Span.SequenceEqual(AssConstants.SectionHeadersBytes.ScriptInfo))
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
            AssEventTextParser.Logger = previousLogger;
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

        if (sp[0] == AssConstants.SectionHeaderStartByte && sp[^1] == AssConstants.SectionHeaderEndByte)
        {
            logger?.ZLogInformation($"Start parse section {Utils.GetString(sp)}");
            if (sp.SequenceEqual(AssConstants.SectionHeadersBytes.ScriptInfo))
            {
                sectionType = AssSection.ScriptInfo;
            }
            else if (sp.SequenceEqual(AssConstants.SectionHeadersBytes.StyleV4P))
            {
                sectionType = AssSection.StylesV4P;
            }
            else if (sp.SequenceEqual(AssConstants.SectionHeadersBytes.StyleV4))
            {
                sectionType = AssSection.StylesV4;
            }
            else if (sp.SequenceEqual(AssConstants.SectionHeadersBytes.StyleV4PP))
            {
                sectionType = AssSection.StylesV4PP;
            }
            else if (sp.SequenceEqual(AssConstants.SectionHeadersBytes.Events))
            {
                sectionType = AssSection.Events;
            }
            else if (sp.SequenceEqual(AssConstants.SectionHeadersBytes.Fonts))
            {
                sectionType = AssSection.Fonts;
            }
            else if (sp.SequenceEqual(AssConstants.SectionHeadersBytes.Graphics))
            {
                sectionType = AssSection.Graphics;
            }
            else if (sp.SequenceEqual(AssConstants.SectionHeadersBytes.AegisubProjectGarbage))
            {
                sectionType = AssSection.AegisubProjectGarbage;
            }
            else if (sp.SequenceEqual(AssConstants.SectionHeadersBytes.AegisubExtradata))
            {
                sectionType = AssSection.AegisubExtradata;
            }
            else
            {
                if (AllowUnknownSections)
                {
                    sectionType = AssSection.None;
                    return;
                }

                throw new Exception($"Unknown section: {Utils.GetString(sp)}.");
            }

            if (sectionType != AssSection.None && !Sections.Add(sectionType))
            {
                throw new Exception($"Duplicate section: {Utils.GetString(sp)}");
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
