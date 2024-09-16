using System.Buffers;
using System.Diagnostics;
using System.Text;
using Microsoft.Extensions.Logging;
using ZLogger;
using Mobsub.SubtitleParse.AssTypes;

namespace Mobsub.SubtitleParse.AssUtils;

public partial class AssTagParse2(AssStyles styles, AssScriptInfo scriptInfo, ILogger? logger = null)
{
    public bool useVSFmod = false;

    // private bool _parseTag = false;
    // private bool _parseValue = false;
    private SearchValues<char> _sepValues = SearchValues.Create($"{AssConstants.BackSlash}{AssConstants.StartValueBlock}");

    // private Dictionary<string, string> ovrTags = [];
    private bool inTransformation = false;
    // private bool setLineAlignment = false;
    // private bool setLinePosition = false;
    // private bool setLineFade = false;
    private bool drawingMode = false;
    private StringBuilder drawingText = new ();
    
    private HashSet<string> curLineTags = [];
    private HashSet<string> curBlockTags = [];
    private HashSet<string> curTransTags = [];
    private HashSet<string> preTransTags = [];

    private HashSet<string> resetStyles = [];

    private AssTextStyle? curTextStyle;
    private AssTagTransform? curTextStyleTrans;
    // private List<AssTextStyleTrans> transTextStyles = [];
    
    public void Parse(ReadOnlySpan<char> block, AssStyle style)
    {
        if (!inTransformation)
        {
            curTextStyle = new AssTextStyle(style);
            if (AssEvent.IsOverrideBlock(block) && block.Length > 2)
            {
                block = block[1..^1];
            }
        }

        var len = block.Length;
        int charConsumed;

        for (var i = 0; i < len; i += charConsumed)
        {
            charConsumed = 1;
            var parseTag = block[i] switch
            {
                AssConstants.BackSlash => true,
                _ => false
            };
            
            if (!parseTag) { continue; }
            var startPos = i + 1;
            var next = block[startPos..].IndexOfAny(_sepValues);

            int endPos;
            if (next >= 0)
            {
                endPos = startPos + next;

                if (block[endPos] == AssConstants.StartValueBlock)
                {
                    var valueEnd = block[endPos..].IndexOf(AssConstants.EndValueBlock);
                    if (valueEnd >= 0)
                    {
                        endPos += valueEnd + 1;
                        next += valueEnd + 1;
                    }
                }
                
                charConsumed += next;
            }
            else
            {
                endPos = len;
                charConsumed = len - i;
            }

            var tag = block[startPos..endPos];
            Debug.WriteLine(tag.ToString());
            ParseTag(tag);
        }
    }

    public AssTextStyle? GetTextStyle() => curTextStyle;
    public HashSet<string> GetResetStyles() => resetStyles;
    public void ResetNewBlock()
    {
        if (curBlockTags.Count != 0)
        {
            curLineTags.UnionWith(curBlockTags);
        }
        curBlockTags.Clear();
    }
    public void ResetNewLine()
    {
        curTextStyle = null;
        curBlockTags.Clear();
        curLineTags.Clear();
    }

    public IEnumerable<Dictionary<AssTextStyle, List<Rune>>> ParseEvents(AssEvents evts)
    {
        foreach (var evt in evts.Collection)
        {
            Debug.WriteLine(evt.lineNumber);
            if (evt.WillSkip()){ continue; }

            styles.TryGetStyleWithFallback(evt.Style.AsSpan(), out var style);

            if (evt.TextRanges.Length == 0)
            {
                evt.UpdateTextRanges();
            }

            Dictionary<AssTextStyle, List<Rune>> dict = [];
            var i = 0;
            foreach (var range in evt.TextRanges)
            {
                i += 1;
                var sp = evt.Text.AsSpan()[range];
                Debug.WriteLine(sp.ToString());
                List<Rune> curRunes = [];
                
                if (drawingMode)
                {
                    drawingText.Append(sp);
                    continue;
                }
                
                if (AssEvent.IsOverrideBlock(sp))
                {
                    if (i == evt.TextRanges.Length) { continue; }
                    Parse(sp, style!);
                }
                else
                {
                    if (AssEvent.IsEventSpecialCharPair(sp))
                    {
                        switch (sp[1])
                        {
                            case 'h':
                                curRunes.Add(new Rune(AssConstants.NoBreakSpaceUtf16));
                                break;
                        }
                    }
                    else
                    {
                        DecodeCharsToRunes(sp, curRunes);
                    }

                    var textStyle = GetTextStyle();
                    if (textStyle is not null)
                    {
                        if (!dict.TryAdd(textStyle, curRunes))
                        {
                            dict[textStyle].AddRange(curRunes);
                        }
                    }
                    ResetNewBlock();
                }
            }

            if (drawingMode)
            {
                ParseDrawingText();
            }

            yield return dict;
            ResetNewLine();
        }
    }
    
    
    // Position
    private void ParseTagAlignment(ReadOnlySpan<char> span, bool isLegacy)
    {
        if (IsEmptyOrWhiteSpace(span))
        {
            if (span.Length > 0)
            {
                logger?.ZLogWarning($"Useless declaration: {(isLegacy ? AssConstants.OverrideTags.AlignmentLegacy : AssConstants.OverrideTags.Alignment)}{span.ToString()}");
            }
            
            curTextStyle!.Alignment = curTextStyle.BaseStyle.Alignment;
        }
        else
        {
            if (!int.TryParse(span, out var v))
            {
                logger?.ZLogWarning($"Invalid value: {(isLegacy ? AssConstants.OverrideTags.AlignmentLegacy : AssConstants.OverrideTags.Alignment)}{span.ToString()}");
            }

            if (isLegacy)
            {
                if (v is <= 0 or >= 12)
                {
                    logger?.ZLogWarning($"Invalid value: {AssConstants.OverrideTags.AlignmentLegacy}{span.ToString()}");
                    curTextStyle!.Alignment = curTextStyle.BaseStyle.Alignment;
                }
                else
                {
                    // copy from VSFilter
                    curTextStyle!.Alignment = ((v - 1) & 3) + 1 + ((v & 4) != 0 ? 6 : 0) + ((v & 8) != 0 ? 3 : 0);
                }
            }
            else
            {
                if (v is <= 0 or >= 10)
                {
                    logger?.ZLogWarning($"Invalid value: {AssConstants.OverrideTags.Alignment}{span.ToString()}");
                    curTextStyle!.Alignment = curTextStyle.BaseStyle.Alignment;
                }
                else
                {
                    curTextStyle!.Alignment = v;
                }
            }
        }
    }

    private void ParseTagPosition(ReadOnlySpan<char> span)
    {
        if (IsEmptyOrWhiteSpace(span))
        {
            if (span.Length > 0)
            {
                logger?.ZLogWarning($"Extra whitespace: {AssConstants.OverrideTags.Position}{AssConstants.StartValueBlock}{span.ToString()}{AssConstants.EndValueBlock}");
            }
        }
        else
        {
            var splitResult = SplitFunctionParams(span, out var ranges);
            if (splitResult == SplitStatus.NoValue || ranges.Count != 2)
            {
                logger?.ZLogWarning($"Useless position: {AssConstants.OverrideTags.Position}{AssConstants.StartValueBlock}{span.ToString()}{AssConstants.EndValueBlock}");
                return;
            }

            var r1 = double.TryParse(span[ranges[0]], out var p1);
            var r2 = double.TryParse(span[ranges[1]], out var p2);
            if (!r1 || !r2)
            {
                logger?.ZLogWarning($"Useless position: {AssConstants.OverrideTags.Position}{AssConstants.StartValueBlock}{span.ToString()}{AssConstants.EndValueBlock}");
            }

            curTextStyle!.Position = new AssTextPosition(p1, p2);
        }
    }
    
    // Font
    private void ParseTagFontName(ReadOnlySpan<char> span)
    {
        if (IsEmptyOrWhiteSpace(span) || span.Trim() == "0")
        {
            if (span.Length > 0)
            {
                logger?.ZLogWarning($"Extra whitespace or zero: {AssConstants.OverrideTags.FontName}{span.ToString()}");
            }
            curTextStyle!.FontName = curTextStyle.BaseStyle.Fontname;
        }
        else
        {
            if (StartOrEndIsWhiteSpace(span))
            {
                logger?.ZLogWarning($"Extra whitespace: {AssConstants.OverrideTags.FontName}{span.ToString()}");
            }
            curTextStyle!.FontName = span.Trim().ToString();
        }
    }
    private void ParseTagBold(ReadOnlySpan<char> span)
    {
        curTextStyle!.FontWeight = curTextStyle.BaseStyle.Bold ? 1 : 0;
        if (IsEmptyOrWhiteSpace(span))
        {
            if (span.Length > 0)
            {
                logger?.ZLogWarning($"Extra whitespace: {AssConstants.OverrideTags.Bold}{span.ToString()}");
            }
        }
        else
        {
            if (!int.TryParse(span, out var v))
            {
                logger?.ZLogWarning($"Invalid value: {AssConstants.OverrideTags.Bold}{span.ToString()}");
            }

            if (v is 0 or 1 or >= 100)
            {
                curTextStyle!.FontWeight = v;
            }
        }
    }
    private void ParseTagFontEncoding(ReadOnlySpan<char> span)
    {
        if (IsEmptyOrWhiteSpace(span))
        {
            if (span.Length > 0)
            {
                logger?.ZLogWarning($"Extra whitespace: {AssConstants.OverrideTags.FontEncoding}{span.ToString()}");
            }

            curTextStyle!.FontEncoding = curTextStyle.BaseStyle.Encoding;
        }
        else
        {
            if (!int.TryParse(span, out var v))
            {
                logger?.ZLogWarning($"Invalid value: {AssConstants.OverrideTags.FontEncoding}{span.ToString()}");
            }

            curTextStyle!.FontEncoding = v;
        }
    }
    private void ParseTagReset(ReadOnlySpan<char> span)
    {
        AssStyle resetStyle;
        if (IsEmptyOrWhiteSpace(span))
        {
            if (span.Length > 0)
            {
                logger?.ZLogWarning($"Extra whitespace: {AssConstants.OverrideTags.Reset}{span.ToString()}");
            }
            
            resetStyle = curTextStyle!.BaseStyle;
        }
        else
        {
            var sylName = span.TrimEnd().ToString();
            resetStyles.Add(sylName);
            resetStyle = styles.TryGetStyle(sylName, out var style) ? style! : curTextStyle!.BaseStyle;
        }
        curTextStyle!.Reset(resetStyle);
    }
    // ParseTagItalic()
    
    // Typesetting
    // ParseTagFontSize()
    // ParseTagStrikeout()
    // ParseTagUnderline()
    private void ParseTagWrapStyle(ReadOnlySpan<char> span)
    {
        if (IsEmptyOrWhiteSpace(span))
        {
            if (span.Length > 0)
            {
                logger?.ZLogWarning($"Extra whitespace: {AssConstants.OverrideTags.WrapStyle}{span.ToString()}");
            }
            curTextStyle!.TextWrapStyle = scriptInfo.WrapStyle;
        }
        else
        {
            if (!int.TryParse(span, out var v))
            {
                logger?.ZLogWarning($"Invalid value: {AssConstants.OverrideTags.WrapStyle}{span.ToString()}");
            }

            if (v is < 0 or > 3)
            {
                logger?.ZLogWarning($"Invalid value: {AssConstants.OverrideTags.WrapStyle}{span.ToString()}");
                curTextStyle!.TextWrapStyle = scriptInfo.WrapStyle;
            }
            else
            {
                curTextStyle!.TextWrapStyle = v;
            }
        }
    }
    
    // Effect
    // ParseTagBlueEdges()
    // ParseTagBlurEdgesGaussian()
    private void ParseTagColor(ReadOnlySpan<char> span, int index, bool isAlpha)
    {
        var colors = inTransformation switch
        {
            true when curTextStyleTrans!.TransTextStyle.Colors is not null => curTextStyleTrans.TransTextStyle.Colors,
            false when curTextStyle!.Colors is not null => curTextStyle.Colors,
            _ => new AssTextColor()
            {
                Primary = curTextStyle!.BaseStyle.PrimaryColour,
                Secondary = curTextStyle!.BaseStyle.SecondaryColour,
                Outline = curTextStyle!.BaseStyle.OutlineColour,
                Back = curTextStyle!.BaseStyle.BackColour,
            }
        };

        if (!isAlpha)
        {
            var c = new AssRGB8();
            c.Parse(span);
            switch (index)
            {
                case 1:
                    c.A = colors.Primary.A;
                    colors.Primary = c;
                    break;
                case 2:
                    c.A = colors.Secondary.A;
                    colors.Secondary = c;
                    break;
                case 3:
                    c.A = colors.Outline.A;
                    colors.Outline = c;
                    break;
                case 4:
                    c.A = colors.Back.A;
                    colors.Back = c;
                    break;
            }
        }
        else
        {
            var a = ParseHexAlpha(span);
            switch (index)
            {
                case 0:
                    colors.Primary.A = a;
                    colors.Secondary.A = a;
                    colors.Outline.A = a;
                    colors.Back.A = a;
                    break;
                case 1:
                    colors.Primary.A = a;
                    break;
                case 2:
                    colors.Secondary.A = a;
                    break;
                case 3:
                    colors.Outline.A = a;
                    break;
                case 4:
                    colors.Back.A = a;
                    break;
            }
        }

        if (inTransformation)
        {
            curTextStyleTrans!.TransTextStyle.Colors = colors;
        }
        else
        {
            curTextStyle!.Colors = colors;
        }
    }
    private byte ParseHexAlpha(ReadOnlySpan<char> span)
    {
        if (span[0] == '&' && span[1] == 'H' && span[^1] == '&' && span.Length == 5)
        {
            return Convert.ToByte(AssRGB8.HexCharToInt(span[2]) * 16 + AssRGB8.HexCharToInt(span[3]));
        }

        logger?.ZLogError($"Unknown alpha tags: {span.ToString()}");
        return 0;
    }
    // ParseTagBorder()
    // ParseTagShadow()
    // ParseTagFontSizeScale()
    
    // Drawing
    private void ParseTagPolygon(ReadOnlySpan<char> span)
    {
        if (IsEmptyOrWhiteSpace(span))
        {
            if (span.Length > 0)
            {
                logger?.ZLogWarning($"Extra whitespace: {AssConstants.OverrideTags.Polygon}{span.ToString()}");
            }

            drawingMode = false;
        }
        else
        {
            if (!int.TryParse(span, out var v))
            {
                logger?.ZLogWarning($"Invalid value: {AssConstants.OverrideTags.Polygon}{span.ToString()}");
            }

            if (v <= 0)
            {
                logger?.ZLogWarning($"Invalid value: {AssConstants.OverrideTags.Polygon}{span.ToString()}");
                drawingMode = false;
            }
            else
            {
                curTextStyle!.PolygonScale = v;
                drawingMode = true;
            }
        }
    }
    public void ParseDrawingText()
    {
        
    }

    private void ParseTagClip(ReadOnlySpan<char> span)
    {
        
    }
    
    // Transform
    private void ParseTagTransform(ReadOnlySpan<char> span)
    {
        if (IsEmptyOrWhiteSpace(span))
        {
            if (span.Length > 0)
            {
                logger?.ZLogWarning($"Extra whitespace: {AssConstants.OverrideTags.Transform}{AssConstants.StartValueBlock}{span.ToString()}{AssConstants.EndValueBlock}");
            }
        }
        else
        {
            inTransformation = true;
            var splitResult = SplitTransFunctionParams(span, out var ranges);
            
            switch (splitResult)
            {
                case SplitStatus.None:
                    curTextStyleTrans = new AssTagTransform(logger) { TransTextStyle = new AssTextStyleTransform(curTextStyle!) };
                    break;
                case SplitStatus.NoValue:
                    logger?.ZLogWarning($"Useless transformation: {AssConstants.OverrideTags.Transform}{AssConstants.StartValueBlock}{span.ToString()}{AssConstants.EndValueBlock}");
                    ResetTransformation();
                    return;
            }
            
            switch (ranges.Count)
            {
                case 1:
                    break;
                case 2:
                    curTextStyleTrans!.ParseAccel(span[ranges[0]]);
                    break;
                case 3:
                    curTextStyleTrans!.ParseTime1(span[ranges[0]]);
                    curTextStyleTrans!.ParseTime2(span[ranges[1]]);
                    break;
                case 4:
                    curTextStyleTrans!.ParseTime1(span[ranges[0]]);
                    curTextStyleTrans!.ParseTime2(span[ranges[1]]);
                    curTextStyleTrans!.ParseAccel(span[ranges[2]]);
                    break;
            }
            
            Parse(span[ranges[^1]], curTextStyle!.BaseStyle);
            ResetTransformation();
        }
    }
    private void ResetTransformation()
    {
        inTransformation = false;
        foreach (var tag in curTransTags)
        {
            if (!AssConstants.OverrideTagsAnimateable.Contains(tag))
            {
                curBlockTags.Add(tag);
            }
            else
            {
                preTransTags.Add(tag);
            }
        }
        curTransTags.Clear();

        if (curTextStyleTrans != null)
        {
            if (curTextStyle!.Transform is null)
            {
                curTextStyle!.Transform = [];
            }
            
            curTextStyle!.Transform.Add(curTextStyleTrans);
            curTextStyleTrans = null;
        }
    }
    
    // Utilities
    private static bool IsEmptyOrWhiteSpace(ReadOnlySpan<char> span) => span.IsEmpty || span.IsWhiteSpace();
    private static bool StartOrEndIsWhiteSpace(ReadOnlySpan<char> span) =>
        char.IsWhiteSpace(span[0]) || char.IsWhiteSpace(span[1]);

    private TagDuplicate CheckDuplicateTag(string tag)
    {
        if (inTransformation)
        {
            if (AssConstants.OverrideTagsAnimateable.Contains(tag))
            {
                return !curTransTags.Add(tag) ? TagDuplicate.BlockRenderLatest : TagDuplicate.None;
            }
            
            if (AssConstants.OverrideTagsLineOnlyRenderFirst.Contains(tag))
            {
                if (curLineTags.Contains(tag) || curBlockTags.Contains(tag))
                {
                    return TagDuplicate.InvalidDefinedArea | TagDuplicate.UniqueLineOnlyFirst;
                }

                return TagDuplicate.InvalidDefinedArea | TagDuplicate.None;
            }

            if (curLineTags.Contains(tag) || curBlockTags.Contains(tag))
            {
                return TagDuplicate.InvalidDefinedArea | TagDuplicate.BlockRenderLatest;
            }

            return TagDuplicate.InvalidDefinedArea | TagDuplicate.None;
        }

        var blk = curBlockTags.Add(tag);
        if (AssConstants.OverrideTagsLineOnlyRenderFirst.Contains(tag))
        {
            if (curLineTags.Contains(tag) || !blk)
            {
                return TagDuplicate.UniqueLineOnlyFirst;
            }
        }
        
        if (AssConstants.OverrideTagsLineOnlyRenderLatest.Contains(tag))
        {
            if (curLineTags.Contains(tag) || !blk)
            {
                return TagDuplicate.LineRenderLatest;
            }
        }

        if (preTransTags.Contains(tag))
        {
            return TagDuplicate.BlockRenderLatest;
        }

        return !blk ? TagDuplicate.BlockRenderLatest : TagDuplicate.None;
    }
    private bool CheckDuplicateFlag(ReadOnlySpan<char> span, TagDuplicate flag)
    {
        var pass = false;
        if (flag.HasFlag(TagDuplicate.UniqueLineOnlyFirst))
        {
            logger?.ZLogWarning($"Useless tag: {span.ToString()}");
            pass = true;
        }
        else if (flag.HasFlag(TagDuplicate.InvalidDefinedArea))
        {
            logger?.ZLogWarning($"Invalid use tag: {span.ToString()}");
        }

        return pass;
    }
    private ReadOnlySpan<char> TrimValueBlockSep(ReadOnlySpan<char> span, string tag)
    {
        if (span.Length == 0)
        {
            return span;
        }
        
        if (span[0] == AssConstants.StartValueBlock && span[^1] == AssConstants.EndValueBlock)
        {
            if (!AssConstants.OverrideTagsShouldBeFunction.Contains(tag))
            {
                logger?.ZLogWarning($"Unused value block: \\{tag}{span.ToString()}");
            }
            return span[1..^1];
        }

        if (AssConstants.OverrideTagsShouldBeFunction.Contains(tag))
        {
            logger?.ZLogError($"Must use value block: \\{tag}{span.ToString()}");
        }

        return span;
    }

    private SplitStatus SplitFunctionParams(ReadOnlySpan<char> span, out List<Range> ranges)
    {
        ranges = [];
        var commaIdx = span.IndexOf(AssConstants.FunctionParamSeparator);

        if (commaIdx < 0)
        {
            return SplitStatus.NoValue;
        }
        
        var startIdx = 0;
        while (commaIdx >= 0)
        {
            ranges.Add(new Range(startIdx, commaIdx));
            startIdx = commaIdx + 1;
            commaIdx = span[startIdx..].IndexOf(AssConstants.FunctionParamSeparator);
        }

        return SplitStatus.None;
    }
    private SplitStatus SplitTransFunctionParams(ReadOnlySpan<char> span, out List<Range> ranges)
    {
        var status = SplitStatus.None;
        ranges = [];
        
        var commaIdx = span.IndexOf(AssConstants.FunctionParamSeparator);
        var slashIdx = span.IndexOf(AssConstants.BackSlash);
        var startIdx = 0;
        
        while (commaIdx < slashIdx && commaIdx >= 0)
        {
            ranges.Add(new Range(startIdx, commaIdx));
            startIdx = commaIdx + 1;
            var nextCommaIdx = span[startIdx..].IndexOf(AssConstants.FunctionParamSeparator);
            if (nextCommaIdx < 0)
            {
                break;
            }
            commaIdx = startIdx + nextCommaIdx;
        }
        
        if (slashIdx < 0)
        {
            status = SplitStatus.NoValue;
            ranges.Add(Range.StartAt(0));
        }
        else
        {
            ranges.Add(Range.StartAt(slashIdx));
        }

        return status;
    }
    
    private static void DecodeCharsToRunes(ReadOnlySpan<char> span, List<Rune> curRunes)
    {
        int charsConsumed;
        for (var i = 0; i < span.Length; i += charsConsumed)
        {
            Rune.DecodeFromUtf16(span[i..], out var rune, out charsConsumed);
            curRunes.Add(rune);
        }
    }
    
    // Data
    [Flags]
    private enum TagDuplicate
    {
        None = 0,
        BlockRenderLatest = 0b_1,
        UniqueLineOnlyFirst = 0b_10,
        InvalidDefinedArea = 0b_100,
        LineRenderLatest = 0b_1000,
    }

    private enum SplitStatus
    {
        None = 0,
        NoValue = 1,
    }
    
    
    
    
    
    public List<char[]> SplitTags(ReadOnlySpan<char> block)
    {
        List<char[]> tags = [];
        
        if (AssEvent.IsOverrideBlock(block) && block.Length > 2)
        {
            block = block[1..^1];
        }
        
        var valueStartIndex = block.IndexOf(AssConstants.StartValueBlock);
        var preEndIndex = -1;
        var preOvrTagsEnd = -1;

        while (valueStartIndex != -1)
        {
            preOvrTagsEnd = block[..valueStartIndex].LastIndexOf(AssConstants.BackSlash);
        }
        
        SplitOnlyOvrTags(block, tags);
        return tags;

    }

    /// <summary>
    /// split override tags, maybe return empty when have consecutive backslash or bracket
    /// </summary>
    private void SplitOnlyOvrTags(ReadOnlySpan<char> block, List<char[]> cal, bool ingoreConsecutive)
    {
        if (block.IsEmpty)
        {
            return ;
        }
        var startIndex = block.IndexOf(AssConstants.BackSlash);

        while (startIndex != -1)
        {
            var endIndex = block[(startIndex + 1)..].IndexOf(AssConstants.BackSlash);
            if (endIndex != -1)
            {
                block = block.Slice(startIndex + 1, endIndex);
                startIndex += endIndex + 1;
            }
            else
            {
                block = block[(startIndex + 1)..];
                break;
            }
            
            if (ingoreConsecutive)
            {
                if (block.Length > 0)
                {
                    cal.Add(block.ToArray());
                }
            }
        }

        if (ingoreConsecutive)
        {
            if (block.Length > 0)
            {
                cal.Add(block.ToArray());
            }
        }
    }
    private void SplitOnlyOvrTags(ReadOnlySpan<char> block, List<char[]> cal) => SplitOnlyOvrTags(block, cal, true);
    
    private void GetTagsFromFunction(ReadOnlySpan<char> block, List<char[]> cal, out ReadOnlySpan<char> function)
    {
        var openFunc = block.IndexOf(AssConstants.StartValueBlock);
        function = block[..openFunc];
        var closeFine = block.EndsWith(")".AsSpan());
        var startIndex = block.IndexOf(AssConstants.BackSlash);
        if (startIndex != -1)
        {
            var slice = closeFine ? block[startIndex..^1].Trim() : block[startIndex..].Trim();
            SplitOnlyOvrTags(slice, cal);
        }
        else
        {
            cal.Add(block.ToArray());
        }
    }
    
    public List<char[]> GetTagsFromTransFunction(ReadOnlySpan<char> block)
    {
        List<char[]> cal = [];
        GetTagsFromFunction(block, cal, out var function);
        if (function.Length != 1 && function[0] != 't')
        {
            throw new Exception($"Invalid transformation function: {block.ToString()}");
        }
        return cal;
    }
}