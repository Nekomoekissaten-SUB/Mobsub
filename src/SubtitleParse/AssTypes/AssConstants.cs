﻿using System;
using System.Collections.Generic;
using System.Text;

namespace Mobsub.SubtitleParse.AssTypes;

public class AssConstants
{
    public const string FormatLinePrefix = "Format: ";
    public const byte CommentLinePrefixByte = (byte)';';
    public const byte SectionHeaderStartByte = (byte)'[';

    public static class Text
    {
        public const char Escape = '\\';
        public const byte EscapeByte = (byte)'\\';
        public const string EscapeString = "\\";

        public const char OverrideBlockStart = '{';
        public const byte OverrideBlockStartByte = (byte)'{';

        public const char OverrideBlockEnd = '}';
        public const byte OverrideBlockEndByte = (byte)'}';

        public const char HardLineBreak = 'N';
        public const char SoftLineBreak = 'n';
        public const char HardSpace = 'h';

        public const char CarriageReturn = '\r';
        public const char LineFeed = '\n';

        public const char Comma = ',';
        public const byte CommaByte = (byte)',';

        public const char OpenParen = '(';
        public const char CloseParen = ')';
        public const char Space = ' ';
        public const char Tab = '\t';

        public const string AssHardLineBreak = "\\N";

        public static bool IsEventTextSpecialEscape(char c)
            => c is HardLineBreak or SoftLineBreak or HardSpace or Escape or OverrideBlockStart or OverrideBlockEnd;

        public static bool IsOverrideCompletionWordBoundary(char c)
            => c == Escape || c == Comma || c == OpenParen || c == CloseParen || c == OverrideBlockStart || c == OverrideBlockEnd || char.IsWhiteSpace(c);
    }

    public const string SectionScriptInfo = "[Script Info]";
    public const string SectionStyleV4 = "[V4 Styles]";
    public const string SectionStyleV4P = "[V4+ Styles]";
    public const string SectionStyleV4PP = "[V4++ Styles]";
    public const string SectionEvent = "[Events]";
    public const string SectionFonts = "[Fonts]";
    public const string SectionGraphics = "[Graphics]";
    public const string SectionAegisubProjectGarbage = "[Aegisub Project Garbage]";
    public const string SectionAegisubExtradata = "[Aegisub Extradata]";

    public static class AegisubProjectGarbageKeys
    {
        public const string ExportFilters = "Export Filters";
    }

    public static class BooleanLiterals
    {
        public const string Yes = "yes";
        public const string No = "no";

        public static ReadOnlySpan<byte> YesBytes => "yes"u8;
        public static ReadOnlySpan<byte> NoBytes => "no"u8;
    }

    public const string ScriptTypeV4P = "v4.00+";
    public const string ScriptTypeV4PP = "v4.00++";
    public const string ScriptTypeV4 = "v4.00";

    public static class ScriptTypeBytes
    {
        public static ReadOnlySpan<byte> V4 => "v4.00"u8;
        public static ReadOnlySpan<byte> V4P => "v4.00+"u8;
        public static ReadOnlySpan<byte> V4PP => "v4.00++"u8;
    }

    public static class SectionHeadersBytes
    {
        public static ReadOnlySpan<byte> ScriptInfo => "[Script Info]"u8;
        public static ReadOnlySpan<byte> StyleV4 => "[V4 Styles]"u8;
        public static ReadOnlySpan<byte> StyleV4P => "[V4+ Styles]"u8;
        public static ReadOnlySpan<byte> StyleV4PP => "[V4++ Styles]"u8;
        public static ReadOnlySpan<byte> Events => "[Events]"u8;
        public static ReadOnlySpan<byte> Fonts => "[Fonts]"u8;
        public static ReadOnlySpan<byte> Graphics => "[Graphics]"u8;
        public static ReadOnlySpan<byte> AegisubProjectGarbage => "[Aegisub Project Garbage]"u8;
        public static ReadOnlySpan<byte> AegisubExtradata => "[Aegisub Extradata]"u8;
    }

    public const string StyleFormatV4 = "Name, Fontname, Fontsize, PrimaryColour, SecondaryColour, TertiaryColour, BackColour, Bold, Italic, BorderStyle, Outline, Shadow, Alignment, MarginL, MarginR, MarginV, AlphaLevel, Encoding";
    public const string StyleFormatV4P = "Name, Fontname, Fontsize, PrimaryColour, SecondaryColour, OutlineColour, BackColour, Bold, Italic, Underline, StrikeOut, ScaleX, ScaleY, Spacing, Angle, BorderStyle, Outline, Shadow, Alignment, MarginL, MarginR, MarginV, Encoding";
    public const string StyleFormatV4PP = "Name, Fontname, Fontsize, PrimaryColour, SecondaryColour, OutlineColour, BackColour, Bold, Italic, Underline, StrikeOut, ScaleX, ScaleY, Spacing, Angle, BorderStyle, Outline, Shadow, Alignment, MarginL, MarginR, MarginT, MarginB, Encoding, RelativeTo";
    public static readonly byte[] StyleDefaultV4P = "Style: Default,Arial,48,&H00FFFFFF,&H000000FF,&H00000000,&H00000000,0,0,0,0,100,100,0,0,1,2,2,2,10,10,10,1"u8.ToArray();

    public const string EventFormatV4 = "Marked, Start, End, Style, Name, MarginL, MarginR, MarginV, Effect, Text";
    public const string EventFormatV4P = "Layer, Start, End, Style, Name, MarginL, MarginR, MarginV, Effect, Text";
    public const string EventFormatV4PP = "Layer, Start, End, Style, Name, MarginL, MarginR, MarginT, MarginB, Effect, Text";

    public static class EmbeddedSectionHeaderKeys
    {
        public const string FontName = "fontname";
        public const string FileName = "filename";
    }

    public static class EventFields
    {
        public const string Layer = "Layer";
        public const string Marked = "Marked";
        public const string Start = "Start";
        public const string End = "End";
        public const string Style = "Style";
        public const string Name = "Name";
        public const string MarginL = "MarginL";
        public const string MarginR = "MarginR";
        public const string MarginV = "MarginV";
        public const string MarginT = "MarginT";
        public const string MarginB = "MarginB";
        public const string Effect = "Effect";
        public const string Text = "Text";
    }

    public static class EventsLineHeaders
    {
        public static ReadOnlySpan<byte> Semicolon => ";"u8;
        public static ReadOnlySpan<byte> Format => "Format"u8;
        public static ReadOnlySpan<byte> Dialogue => "Dialogue"u8;
        public static ReadOnlySpan<byte> Comment => "Comment"u8;
    }

    public static class StyleFields
    {
        public const string Name = "Name";
        public const string Fontname = "Fontname";
        public const string Fontsize = "Fontsize";
        public const string PrimaryColour = "PrimaryColour";
        public const string SecondaryColour = "SecondaryColour";
        public const string OutlineColour = "OutlineColour";
        public const string BackColour = "BackColour";
        public const string Bold = "Bold";
        public const string Italic = "Italic";
        public const string Underline = "Underline";
        public const string StrikeOut = "StrikeOut";
        public const string ScaleX = "ScaleX";
        public const string ScaleY = "ScaleY";
        public const string Spacing = "Spacing";
        public const string Angle = "Angle";
        public const string BorderStyle = "BorderStyle";
        public const string Outline = "Outline";
        public const string Shadow = "Shadow";
        public const string Alignment = "Alignment";
        public const string MarginL = "MarginL";
        public const string MarginR = "MarginR";
        public const string MarginV = "MarginV";
        public const string MarginT = "MarginT";
        public const string MarginB = "MarginB";
        public const string AlphaLevel = "AlphaLevel";
        public const string Encoding = "Encoding";
        public const string RelativeTo = "RelativeTo";
    }

    public static class StylesLineHeaders
    {
        public const byte CommentLinePrefixByte = (byte)'/';
        public static ReadOnlySpan<byte> CommentSlash => "/"u8;
        public static ReadOnlySpan<byte> Format => "Format"u8;
        public static ReadOnlySpan<byte> Style => "Style"u8;
    }

    public static class StyleNames
    {
        public const char HiddenPrefix = '*';
        public const string DefaultString = "Default";
        public const string DefaultLowerString = "default";

        public static ReadOnlySpan<byte> DefaultLower => "default"u8;
        public static ReadOnlySpan<byte> Default => "Default"u8;
    }

    public static class ScriptInfo
    {
        // Functional Headers
        public const string ScriptType = "ScriptType";
        public const string PlayResX = "PlayResX";
        public const string PlayResY = "PlayResY";
        public const string LayoutResX = "LayoutResX";
        public const string LayoutResY = "LayoutResY";
        public const string WrapStyle = "WrapStyle";
        public const string Timer = "Timer";
        public const string ScaledBorderAndShadow = "ScaledBorderAndShadow";
        public const string Kerning = "Kerning";    // unused?
        public const string YCbCrMatrix = "YCbCr Matrix";

        // Informational Headers
        public const string Title = "Title";
        public const string OriginalScript = "Original Script";
        public const string OriginalTranslation = "Original Translation";
        public const string OriginalEditing = "Original Editing";
        public const string OriginalTiming = "Original Timing";
        public const string ScriptUpdatedBy = "Script Updated By";
        public const string UpdateDetails = "Update Details";

    }
}
