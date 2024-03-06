using Mobsub.AssTypes;
using Mobsub.Utils;
using System.Diagnostics;
using static Mobsub.Utils.ParseHelper;

namespace Mobsub.SubtitleParse;

public class AssParse
{
    public static AssData ReadAssFile(FileStream fs)
    {
        var assData = new AssData { };

        using var sr = new StreamReader(fs);
        string? line;
        var lineNumber = 0;
        var parseFunc = AssSection.ScriptInfo;
        DetectEncoding.GuessEncoding(fs, out assData.CharEncoding, out assData.CarriageReturn);

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
                parseFunc = line switch
                {
                    "[Script Info]" => AssSection.ScriptInfo,
                    "[V4 Styles]" => AssSection.StylesV4,
                    "[V4+ Styles]" => AssSection.StylesV4P,
                    "[V4++ Styles]" => AssSection.StylesV4PP,
                    "[Events]" => AssSection.Events,
                    "[Fonts]" => AssSection.Fonts,
                    "[Graphics]" => AssSection.Graphics,
                    "[Aegisub Project Garbage]" => AssSection.AegisubProjectGarbage,
                    "[Aegisub Extradata]" => AssSection.AegisubExtradata,
                    _ => throw new Exception($"Unknown section: {line}."),
                };

                if (!assData.Sections.Add(parseFunc))
                {
                    throw new Exception($"Duplicate section: {line}");
                }
                continue;
            }

            switch (parseFunc)
            {
                case AssSection.ScriptInfo:
                    assData.ScriptInfo.Read(sp);
                    break;
                case AssSection.StylesV4:
                case AssSection.StylesV4P:
                case AssSection.StylesV4PP:
                    assData.Styles.Read(sp);
                    break;
                case AssSection.Events:
                    assData.Events.Read(sp, assData.ScriptInfo.ScriptType, lineNumber);
                    break;
                case AssSection.AegisubProjectGarbage:
                    TrySplitKeyValue(sp, out var k, out var v);
                    assData.AegisubProjectGarbage.TryAdd(k, v);
                    break;
                case AssSection.AegisubExtradata:
                    TrySplitKeyValue(sp, out var k1, out var v1);
                    assData.AegiusbExtradata.Add(v1);
                    break;
                case AssSection.Fonts:
                    ParseFonts(sp, assData.Fonts);
                    break;
                case AssSection.Graphics:
                    ParseGraphics(sp, assData.Graphics);
                    break;
                default:
                    break;
            }
        }

        return assData;
    }

    private static void ParseFonts(ReadOnlySpan<char> sp, List<AssEmbeddedFont> fonts)
    {
        if (sp.StartsWith("fontname:"))
        {
            var eFont = new AssEmbeddedFont();
            var startIdx = "fontname:".Length + 1;  // fontname: chaucer_B0.ttf
            var lastSepIdx = sp.LastIndexOf('_');
            var lastSeg = sp[(lastSepIdx + 1)..];
            var noAddition = false;

            int encoding;
            var bPos = lastSeg.IndexOf('B');
            var iPos = lastSeg.IndexOf('I');
            if (bPos == 0)
            {
                eFont.Bold = true;
                if (iPos == 1)
                {
                    eFont.Italic = true;
                    if (int.TryParse(lastSeg[2..], out encoding))
                    {
                        eFont.CharacterEncoding = encoding;
                    }
                    else
                    {
                        noAddition = true;
                    }
                }
                else if (int.TryParse(lastSeg[1..], out encoding))
                {
                    eFont.CharacterEncoding = encoding;
                }
                else
                {
                    noAddition = true;
                }
            }
            else if (iPos == 0)
            {
                eFont.Italic = true;
                if (int.TryParse(lastSeg[1..], out encoding))
                {
                    eFont.CharacterEncoding = encoding;
                }
                else
                {
                    noAddition = true;
                }
            }
            else if (int.TryParse(lastSeg, out encoding))
            {
                eFont.CharacterEncoding = encoding;
            }
            else
            {
                noAddition = true;
            }

            eFont.OriginalName = noAddition ? sp[startIdx..].ToString() : sp[startIdx..lastSepIdx].ToString();

            fonts.Add(eFont);
        }
        else if (fonts.Count > 0)
        {
            var lastFont = fonts[^1];
            lastFont.Data.Add(sp.ToString());
            lastFont.DataLength += sp.Length;
        }
    }
    private static void ParseGraphics(ReadOnlySpan<char> sp, List<AssEmbeddedGraphic> graphics)
    {
        if (sp.StartsWith("fontname:"))
        {
            if (TrySplitKeyValue(sp, out var key, out var value))
                graphics.Add(new AssEmbeddedGraphic() { Name = value });
            else
                throw new Exception($"Please check {sp.ToString()}");
        }
        else
        {
            graphics.Last().Data.Add(sp.ToString());
            graphics.Last().DataLength += sp.Length;
        }
    }

    public static AssData ReadAssFile(string filePath)
    {
        using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read);
        return ReadAssFile(fs);
    }

    private static List<char[]> ParseEventTextOld(ReadOnlySpan<char> span)
    {
        var blk = false;
        List<char[]> records = [];
        char c;
        int _start = 0;
        int _end = 0;
        var backslash = false;


        for (var i = 0; i < span.Length; i++)
        {
            if (i == span.Length - 1 && span[i] != '}')
            {
                // In gerneral, blocks are arranged in this order: (text block -) ovr block - text block - ovr block( - text block)
                // but sometimes, text block will be mistakenly identified as ovr block because it start with open brace but does not have close brace until line end
                // It should be a type error, but parse will ignore it
                // Not considering libass extensions, such as \{ and \}
                if (blk)
                {
                    records.Add(span[_start..].ToArray());
                }
                else
                {
                    records.Add(span[_end..].ToArray());
                }
            }
            else
            {
                c = span[i];
                switch (c)
                {
                    case AssConstants.StartOvrBlock:
                        if (!blk)
                        {
                            if (i > 0 && _end != i)
                            {
                                records.Add(span[_end..i].ToArray());
                            }
                            _start = i;
                            blk = true;
                        }
                        break;
                    case AssConstants.EndOvrBlock:
                        if (blk)
                        {
                            _end = i + 1;
                            records.Add(span[_start.._end].ToArray());
                            blk = false;
                        }
                        break;
                    case AssConstants.BackSlash:
                        if (!blk)
                        {
                            _start = i;
                            backslash = true;
                            if (_end != _start)
                            {
                                records.Add(span[_end.._start].ToArray());
                            }
                            _end = _start;
                        }
                        break;
                    case AssConstants.NBSP or AssConstants.WordBreaker or AssConstants.LineBreaker:
                        if (backslash)
                        {
                            _end = i + 1;
                            backslash = false;
                            records.Add(span[_start.._end].ToArray());
                        }
                        break;
                    default:
                        backslash = false;
                        break;
                }
            }
        }
        
        Debug.Assert(records.Sum(l => l.Length) == span.Length, $"Parse records length is {records.Sum(l => l.Length)}, should be {span.Length}");

        return records;
    }
    
    public static void WriteAssFile(AssData data, string filePath, bool forceEnv, bool ctsRounding)
    {
        var newline = forceEnv ? [.. Environment.NewLine] : (data.CarriageReturn ? new char[]{'\r', '\n'} : ['\n']);
        var charEncoding = forceEnv ? DetectEncoding.EncodingRefOS() : data.CharEncoding;

        using FileStream fileStream = new(filePath, FileMode.Create, FileAccess.Write);
        using var memStream = new MemoryStream();
        using var sw = new StreamWriter(memStream, charEncoding);
        foreach (var s in data.Sections)
        {
            switch (s)
            {
                case AssSection.ScriptInfo:
                    data.ScriptInfo.Write(sw, newline);
                    break;
                case AssSection.StylesV4P:
                case AssSection.StylesV4:
                    data.Styles.Write(sw, newline, data.ScriptInfo.ScriptType);
                    break;
                case AssSection.Events:
                    data.Events.Write(sw, newline, ctsRounding);
                    break;
                case AssSection.Fonts:
                    sw.Write("[Fonts]");
                    sw.Write(newline);
                    foreach (var o in data.Fonts)
                    {
                        o.Write(sw, newline);
                    }
                    break;
                case AssSection.Graphics:
                    sw.Write("[Graphics]");
                    sw.Write(newline);
                    foreach (var o in data.Fonts)
                    {
                        o.Write(sw, newline);
                    }
                    break;
                case AssSection.AegisubProjectGarbage:
                    sw.Write("[Aegisub Project Garbage]");
                    sw.Write(newline);
                    foreach (var kvp in data.AegisubProjectGarbage)
                    {
                        sw.Write($"{kvp.Key}: {kvp.Value}");
                        sw.Write(newline);
                    }
                    sw.Write(newline);
                    break;
                case AssSection.AegisubExtradata:
                    sw.Write("[Aegisub Extradata]");
                    sw.Write(newline);
                    for (var i = 0; i < data.AegiusbExtradata.Count; i++)
                    {
                        sw.Write(data.AegiusbExtradata.ToArray()[i]);
                        sw.Write(newline);
                    }
                    sw.Write(newline);
                    break;
            }
        }
        sw.Flush();

        memStream.Seek(0, SeekOrigin.Begin);
        memStream.CopyTo(fileStream);
        fileStream.Close();
    }
    public static void WriteAssFile(AssData data, string filePath) => WriteAssFile(data, filePath, false, false);

}