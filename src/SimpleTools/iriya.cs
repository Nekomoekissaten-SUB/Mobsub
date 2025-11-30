#:package Microsoft.Windows.CsWin32@0.3.257
#:package ZLogger@2.5.10
#:property CsWin32RunAsBuildTask=true
#:property DisableRuntimeMarshalling=true
#:project ../SubtitleParseNT/Mobsub.SubtitleParseNT2.csproj

// Port from https://github.com/SAPikachu/iriya

using System.Runtime.Versioning;
using System.Text;
using Windows.Win32;
using Windows.Win32.Graphics.Gdi;
using Mobsub.SubtitleParseNT2.AssUtils;
using Mobsub.SubtitleParseNT2.AssTypes;

if (!OperatingSystem.IsWindowsVersionAtLeast(5, 0)){ throw new PlatformNotSupportedException("This tool only runs on Windows."); }

foreach (var arg in args)
{
    await AssCheck(arg);
}

static async Task AssCheck(string assFile)
{
    var assFontProcessor = await GetFontProcessor(assFile);
    var assFontInfos = assFontProcessor.GetResultsWithLineNumbers();
    var firstEventLineNumber = assFontProcessor.FirstEventLineNumber;

    foreach ((var afi, var ri) in assFontInfos)
    {
        using var fontContext = new NativeFontContext(afi.Name, afi.Weight, afi.Italic);
        if (fontContext.Missing) continue;
        var missing = fontContext.Check([.. ri.Keys]);
        if (missing.Length == 0) continue;

        foreach (var r in missing)
        {
            Console.Error.WriteLine($"AssFontInfo '{afi}' Dialogue #{string.Join(", ", ri[r].Select(x => x - firstEventLineNumber + 1))} is missing characters: {r}");
        }
    }
}

static async Task<AssFontProcessor> GetFontProcessor(string assFile)
{
    var ass = new AssData();
    await ass.ReadAssFileAsync(assFile);
    var processor = new AssFontProcessor(ass.ScriptInfo.WrapStyle, ass.Styles) { AnalyzeWithEncoding = true };
    processor.GetUsedFontInfos(ass.Events!);
    return processor;
}

[SupportedOSPlatform("windows5.0")]
internal class NativeFontContext : IDisposable
{
    private HDC _dc;
    private DeleteObjectSafeHandle _font;
    private HGDIOBJ _oldFont;
    private const int LF_FACESIZE = 32;

    private const uint DEFAULT_PITCH_AND_FAMILY = 0;
    private const uint GGI_MARK_NONEXISTING_GLYPHS = 1;
    public bool Missing = false;

    // change bold from bool to int
    public NativeFontContext(string name, int bold, bool italics)
    {
        _dc = PInvoke.CreateCompatibleDC(new HDC());

        var fallbackFont = CreateFontSimple(name + "-NONEXISTENT-" + Guid.NewGuid().ToString(), bold, italics);
        HGDIOBJ prevFallback = default;
        try
        {
            var added = false;
            try
            {
                fallbackFont.DangerousAddRef(ref added);
                var fh = fallbackFont.DangerousGetHandle();
                prevFallback = PInvoke.SelectObject(_dc, new HGDIOBJ(fh));
            }
            finally
            {
                if (added) fallbackFont.DangerousRelease();
            }

            var fallbackBuf = new char[LF_FACESIZE];
            PInvoke.GetTextFace(_dc, fallbackBuf.AsSpan());
            var fallbackFontName = new string(fallbackBuf).TrimEnd('\0');

            PInvoke.SelectObject(_dc, prevFallback);
            fallbackFont.Dispose();

            _font = CreateFontSimple(name, bold, italics);
            bool added2 = false;
            try
            {
                _font.DangerousAddRef(ref added2);
                IntPtr fh2 = _font.DangerousGetHandle();
                _oldFont = PInvoke.SelectObject(_dc, new HGDIOBJ(fh2));
            }
            finally
            {
                if (added2) _font.DangerousRelease();
            }

            var nameBuf = new char[LF_FACESIZE];
            PInvoke.GetTextFace(_dc, nameBuf.AsSpan());
            var selectedName = new string(nameBuf).TrimEnd('\0');

            if (string.Equals(fallbackFontName, selectedName, StringComparison.Ordinal) && !string.Equals(name, fallbackFontName, StringComparison.OrdinalIgnoreCase))
            {
                Missing = true;
                Console.Error.WriteLine($"Warning: Font {name} is either not installed or the default font in this system.");
            }
        }
        catch
        {
            fallbackFont?.Dispose();
            throw;
        }
    }

    private static DeleteObjectSafeHandle CreateFontSimple(string name, int bold, bool italics)
    {
        var weight = bold switch
        {
            0 => FONT_WEIGHT.FW_NORMAL,
            1 => FONT_WEIGHT.FW_BOLD,
            _ => (FONT_WEIGHT)bold,
        };
        return PInvoke.CreateFont(0, 0, 0, 0, (int)weight, italics ? 1U : 0U, 0, 0, FONT_CHARSET.DEFAULT_CHARSET, FONT_OUTPUT_PRECISION.OUT_DEFAULT_PRECIS, FONT_CLIP_PRECISION.CLIP_DEFAULT_PRECIS, FONT_QUALITY.DEFAULT_QUALITY, DEFAULT_PITCH_AND_FAMILY, name);
    }

    public List<char> Check(string str)
    {
        var missing = new List<char>();
        if (string.IsNullOrEmpty(str)) return missing;

        var outBuffer = new ushort[str.Length + 1];
        PInvoke.GetGlyphIndices(_dc, str, outBuffer.AsSpan(), GGI_MARK_NONEXISTING_GLYPHS);

        for (int i = 0; i < str.Length; i++)
        {
            if (outBuffer[i] == 0xffff)
                missing.Add(str[i]);
        }

        return missing;
    }

    public Rune[] Check(Rune[] runes)
    {
        var missing = new List<Rune>();
        if (runes.Length == 0) return [.. missing];

        var sb = new StringBuilder();
        foreach (var rune in runes)
        {
            sb.Append(rune.ToString());
        }
        var str = sb.ToString();
        var span = str.AsSpan();

        var outBuffer = new ushort[str.Length + 1];
        PInvoke.GetGlyphIndices(_dc, str, outBuffer.AsSpan(), GGI_MARK_NONEXISTING_GLYPHS);

        for (int i = 0; i < span.Length; i++)
        {
            // record all surrogates
            if (char.IsHighSurrogate(span[i]) && i + 1 < span.Length && char.IsLowSurrogate(span[i + 1]))
            {
                missing.Add(new Rune(span[i], span[i + 1]));
                i++;
                continue;
            }
            
            if (outBuffer[i] == char.MaxValue)
                missing.Add(new Rune(span[i]));
        }

        return [.. missing];
    }

    public void Dispose()
    {
        try
        {
            if (!_oldFont.IsNull)
            {
                PInvoke.SelectObject(_dc, _oldFont);
            }
        }
        catch { }

        if (_font is object && !_font.IsInvalid)
        {
            _font.Dispose();
        }

        if (!_dc.IsNull)
        {
            PInvoke.DeleteDC(_dc);
        }
    }
}
