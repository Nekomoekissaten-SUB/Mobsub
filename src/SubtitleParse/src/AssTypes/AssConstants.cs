using System.Numerics;

namespace Mobsub.SubtitleParse.AssTypes;

public static partial class AssConstants
{
    public const char StartOvrBlock = '{';
    public const char EndOvrBlock = '}';
    public const char BackSlash = '\\';
    public const char LineBreaker = 'N';
    public const char WordBreaker = 'n';
    public const char NoBreakSpace = 'h';
    public const char Comment = ';';
    public const char StartValueBlock = '(';
    public const char EndValueBlock = ')';
    public const char FunctionParamSeparator = ',';
    public const int NoBreakSpaceUtf16 = 0x00A0;
    public const int SpaceUtf16 = 0x0020;

    public class ScriptInfo
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

    public const string FormatV4 = "Marked, Start, End, Style, Name, MarginL, MarginR, MarginV, Effect, Text";
    public const string FormatV4P = "Layer, Start, End, Style, Name, MarginL, MarginR, MarginV, Effect, Text";
    public const string FormatV4PP = "Layer, Start, End, Style, Name, MarginL, MarginR, MarginT, MarginB, Effect, Text";

    public static bool IsEventLine(ReadOnlySpan<char> sp) => sp.StartsWith("Comment") || sp.StartsWith("Dialogue");
    
    public static class OverrideTags
    {
        [AssOverrideTag(typeof(AssTextColor), "ParseTagColor", "1, false", "Colors")]
        [AssTagKind(AssTagKind.BlockOnlyRenderLatest | AssTagKind.Animateable)]
        public const string ColorPrimary  = "1c";
        
        [AssOverrideTag(typeof(AssTextColor), "ParseTagColor", "2, false", "Colors")]
        [AssTagKind(AssTagKind.BlockOnlyRenderLatest | AssTagKind.Animateable)]
        public const string ColorSecondary  = "2c";
        
        [AssOverrideTag(typeof(AssTextColor), "ParseTagColor", "3, false", "Colors")]
        [AssTagKind(AssTagKind.BlockOnlyRenderLatest | AssTagKind.Animateable)]
        public const string ColorBorder  = "3c";
        
        [AssOverrideTag(typeof(AssTextColor), "ParseTagColor", "4, false", "Colors")]
        [AssTagKind(AssTagKind.BlockOnlyRenderLatest | AssTagKind.Animateable)]
        public const string ColorShadow  = "4c";
        
        [AssOverrideTag(typeof(AssTextColor), "ParseTagColor", "1, true", "Colors")]
        [AssTagKind(AssTagKind.BlockOnlyRenderLatest | AssTagKind.Animateable)]
        public const string AlphaPrimary  = "1a";
        
        [AssOverrideTag(typeof(AssTextColor), "ParseTagColor", "2, true", "Colors")]
        [AssTagKind(AssTagKind.BlockOnlyRenderLatest | AssTagKind.Animateable)]
        public const string AlphaSecondary  = "2a";
        
        [AssOverrideTag(typeof(AssTextColor), "ParseTagColor", "3, true", "Colors")]
        [AssTagKind(AssTagKind.BlockOnlyRenderLatest | AssTagKind.Animateable)]
        public const string AlphaBorder  = "3a";
        
        [AssOverrideTag(typeof(AssTextColor), "ParseTagColor", "4, true", "Colors")]
        [AssTagKind(AssTagKind.BlockOnlyRenderLatest | AssTagKind.Animateable)]
        public const string AlphaShadow  = "4a";
        
        [AssOverrideTag(typeof(AssTextColor), "ParseTagColor", "0, true", "Colors")]
        [AssTagKind(AssTagKind.BlockOnlyRenderLatest | AssTagKind.Animateable)]
        public const string Alpha  = "alpha";
        
        [AssOverrideTag(typeof(int), "ParseTagAlignment", "false")]
        [AssTagKind(AssTagKind.LineOnlyRenderFirst)]
        [AssTagGeneralParse("Alignment", false, false)]
        public const string Alignment = "an";
        
        [AssOverrideTag(typeof(int), "ParseTagAlignment", "true", "Alignment")]
        [AssTagKind(AssTagKind.LineOnlyRenderFirst)]
        public const string AlignmentLegacy = "a";
        
        [AssOverrideTag(typeof(double), "ParseTagBlueEdges", "")] // sbyte?
        [AssTagGeneralParse("null", true)]
        [AssTagKind(AssTagKind.BlockOnlyRenderLatest | AssTagKind.Animateable)]
        public const string BlueEdges = "be";
        
        [AssOverrideTag(typeof(double), "ParseTagBlurEdgesGaussian", "")]
        [AssTagGeneralParse("null", true)]
        [AssTagKind(AssTagKind.BlockOnlyRenderLatest | AssTagKind.Animateable)]
        public const string BlurEdgesGaussian = "blur";
        
        [AssOverrideTag(typeof(AssTextBorder), "ParseTagBorder", "0", "Borders")]
        [AssTagKind(AssTagKind.BlockOnlyRenderLatest | AssTagKind.Animateable)]
        [AssTagGeneralParse("Outline", true)]
        public const string Border = "bord";
        
        [AssOverrideTag(typeof(AssTextBorder), "ParseTagBorder", "1", "Borders")]
        [AssTagKind(AssTagKind.BlockOnlyRenderLatest | AssTagKind.Animateable)]
        public const string BorderX = "xbord";
        
        [AssOverrideTag(typeof(AssTextBorder), "ParseTagBorder", "2", "Borders")]
        [AssTagKind(AssTagKind.BlockOnlyRenderLatest | AssTagKind.Animateable)]
        public const string BorderY = "ybord";
        
        [AssOverrideTag(typeof(int), "ParseTagBold", "", "FontWeight")]
        [AssTagKind(AssTagKind.BlockOnlyRenderLatest)]
        public const string Bold = "b";
        
        [AssOverrideTag(typeof(string), "ParseTagClip", "")]
        [AssTagKind(AssTagKind.BlockOnlyRenderLatest | AssTagKind.Animateable | AssTagKind.ShouldBeFunction)]
        public const string Clip = "clip";
        
        // [AssOverrideTag(typeof(int), "ParseClipTag", "true", "Clip")]
        // public const string InverseClip = "iclip";
        
        [AssOverrideTag(typeof(AssTextColor), "ParseTagColor", "1, false", "Colors")]
        [AssTagKind(AssTagKind.BlockOnlyRenderLatest | AssTagKind.Animateable)]
        public const string ColorPrimaryAbbreviation = "c";
        
        // [AssOverrideTag(typeof(double[]), "ParseFade", "")]
        // public const string Fade = "fade";
        // [AssOverrideTag(typeof(double[]), "ParseFad", "")]
        // public const string Fad = "fad";
        // [AssOverrideTag(typeof(double), "ParseFontShift", "x", "FontShiftX")]
        // public const string FontShiftX = "fax";
        // [AssOverrideTag(typeof(double), "ParseFontShift", "y", "FontShiftY")]
        // public const string FontShiftY = "fay";
        
        [AssOverrideTag(typeof(int), "ParseTagFontEncoding", "")]
        [AssTagKind(AssTagKind.BlockOnlyRenderLatest)]
        [AssTagGeneralParse("Encoding", false, false)]
        public const string FontEncoding = "fe";
        
        [AssOverrideTag(typeof(string), "ParseTagFontName", "")]
        [AssTagKind(AssTagKind.BlockOnlyRenderLatest)]
        [AssTagGeneralParse("Fontname", false, false)]
        public const string FontName = "fn";
        
        // [AssOverrideTag(typeof(double[]), "ParseFontRotation", "0", "Rotations")]
        // public const string FontRotationX = "frx";
        // [AssOverrideTag(typeof(double[]), "ParseFontRotation", "1", "Rotations")]
        // public const string FontRotationY = "fry";
        // [AssOverrideTag(typeof(double[]), "ParseFontRotation", "2", "Rotations")]
        // public const string FontRotationZ = "frz";
        // [AssOverrideTag(typeof(double[]), "ParseFontRotation", "-1", "Rotations")]
        // public const string FontRotation = "fr";
        
        [AssOverrideTag(typeof(AssTextScale), "ParseTagFontSizeScale", "1", "TextScale")]
        [AssTagKind(AssTagKind.BlockOnlyRenderLatest | AssTagKind.Animateable)]
        [AssTagGeneralParse("Scale", true)]  // ScaleX
        public const string FontSizeScaleX = "fscx";
        
        [AssOverrideTag(typeof(AssTextScale), "ParseTagFontSizeScale", "2", "TextScale")]
        [AssTagKind(AssTagKind.BlockOnlyRenderLatest | AssTagKind.Animateable)]
        public const string FontSizeScaleY = "fscy";
        
        // public const string Fsc = "fsc";
        
        [AssOverrideTag(typeof(double), "ParseTagSpacing", "", "TextSpacing")]
        [AssTagGeneralParse("Spacing")]
        [AssTagKind(AssTagKind.BlockOnlyRenderLatest | AssTagKind.Animateable)]
        public const string FontSpacing = "fsp";
        
        [AssOverrideTag(typeof(double), "ParseTagFontSize", "", "FontSize")]
        [AssTagGeneralParse("Fontsize", true)]
        [AssTagKind(AssTagKind.BlockOnlyRenderLatest | AssTagKind.Animateable)]
        public const string FontSize = "fs";
        
        [AssOverrideTag(typeof(bool), "ParseTagItalic", "", "FontItalic")]
        [AssTagGeneralParse("Italic")]
        [AssTagKind(AssTagKind.BlockOnlyRenderLatest)]
        public const string Italic = "i";
        
        // public const string Kt = "kt";
        // [AssOverrideTag(typeof(int), "ParseKaraoke", "")]
        // public const string KaraokeO = "ko";
        // [AssOverrideTag(typeof(int), "ParseKaraoke", "")]
        // public const string KaraokeF = "kf";
        // [AssOverrideTag(typeof(int), "ParseKaraoke", "")]
        // public const string KaraokeFSimple = "K";
        // [AssOverrideTag(typeof(int), "ParseKaraoke", "")]
        // public const string Karaoke = "k";
        // [AssOverrideTag(typeof(double[]), "ParseMovement", "")]
        // public const string Movement = "move";
        // [AssOverrideTag(typeof(double[]), "ParseOriginRotation", "")]
        // public const string OriginRotation = "org";
        
        [AssOverrideTag(typeof(Vector2), "ParseTagPosition", "")]
        [AssTagKind(AssTagKind.LineOnlyRenderFirst | AssTagKind.ShouldBeFunction)]
        public const string Position = "pos";
        
        [AssOverrideTag(typeof(int), "ParseTagPolygonBaselineOffset", "")]
        [AssTagGeneralParse("null", false)]
        [AssTagKind(AssTagKind.BlockOnlyRenderLatest)]
        public const string PolygonBaselineOffset = "pbo";
        
        [AssOverrideTag(typeof(int), "ParseTagPolygon", "", "PolygonScale")]
        [AssTagKind(AssTagKind.BlockOnlyRenderLatest)]
        public const string Polygon = "p";
        
        [AssOverrideTag(typeof(int), "ParseTagWrapStyle", "", "TextWrapStyle")]
        [AssTagKind(AssTagKind.LineOnlyRenderLatest)]
        public const string WrapStyle = "q";
        
        // rnd
        
        [AssOverrideTag(null, "ParseTagReset", "")]
        [AssTagKind(AssTagKind.BlockOnlyRenderLatest)]
        public const string Reset = "r";
        
        [AssOverrideTag(typeof(AssTextShadow), "ParseTagShadow", "0", "Shadows")]
        [AssTagKind(AssTagKind.BlockOnlyRenderLatest | AssTagKind.Animateable)]
        [AssTagGeneralParse("Shadow", true)]
        public const string Shadow = "shad";
        
        [AssOverrideTag(typeof(AssTextShadow), "ParseTagShadow", "1", "Shadows")]
        [AssTagKind(AssTagKind.BlockOnlyRenderLatest | AssTagKind.Animateable)]
        public const string ShadowX = "xshad";
        
        [AssOverrideTag(typeof(AssTextShadow), "ParseTagShadow", "2", "Shadows")]
        [AssTagKind(AssTagKind.BlockOnlyRenderLatest | AssTagKind.Animateable)]
        public const string ShadowY = "yshad";
        
        [AssOverrideTag(typeof(bool), "ParseTagStrikeout", "", "TextStrikeOut")]
        [AssTagGeneralParse("StrikeOut")]
        [AssTagKind(AssTagKind.BlockOnlyRenderLatest)]
        public const string Strikeout = "s";
        
        [AssOverrideTag(typeof(List<AssTagTransform>), "ParseTagTransform", "")]
        [AssTagKind(AssTagKind.ShouldBeFunction)]
        public const string Transform = "t";
        
        [AssOverrideTag(typeof(bool), "ParseTagUnderline", "", "TextUnderline")]
        [AssTagGeneralParse("Underline")]
        [AssTagKind(AssTagKind.BlockOnlyRenderLatest)]
        public const string Underline = "u";
        

        // https://sourceforge.net/p/guliverkli2/code/HEAD/tree/src/subtitles/RTS.cpp#l1383
        // libass ass_types.h ass_render.h ass_parse.c
    }
}
#pragma warning disable CS9113
[AttributeUsage(AttributeTargets.Field, AllowMultiple = false)]
public class AssOverrideTagAttribute(Type? propertyType, string parseMethod, string methodParams, string? mapPropName = null) : Attribute;
public class AssTagGeneralParseAttribute(string stylePropertyName, bool limit = false, bool parse = true) : Attribute;
public class AssTagKindAttribute(AssTagKind kind) : Attribute;
#pragma warning restore CS9113

[Flags]
public enum AssTagKind
{
    BlockOnlyRenderLatest = 0,
    LineOnlyRenderFirst = 0b_1,
    Animateable = 0b_10,
    ShouldBeFunction = 0b_100,
    LineOnlyRenderLatest = 0b_1000,
    IsVsFilterMod = 0b_10000,
}