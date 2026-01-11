﻿using FluentAssertions;
using Mobsub.SubtitleParse.AssTypes;

namespace Mobsub.Test;

[TestClass]
public class ParseAssStyleMatch
{
    [TestMethod]
    public void BuildStyleDict()
    {
        var dict = styles.StyleMap.Dictionary;
        
        dict.Keys.Count.Should().Be(3);
        dict["test"u8.ToArray()].FontnameSpan.SequenceEqual("HYXuanSong 85S"u8).Should().BeTrue();
    }

    [TestMethod]
    public void EventStyleMatch_Default() => styles.GetAssStyleByEventStyle("Default"u8).NameSpan.SequenceEqual("*Default"u8).Should().BeTrue();

    [TestMethod]
    public void EventStyleMatch_StarTest()
    {
        var matched = styles.TryGetAssStyleByEventStyle("****test"u8, out var query, out var view);
        matched.Should().BeTrue();
        query.SequenceEqual("test"u8).Should().BeTrue();
        view!.NameSpan.SequenceEqual("*test"u8).Should().BeTrue();
    }

    [TestMethod]
    public void EventStyleMatch_StarDefault()
    {
        var matched = styles.TryGetAssStyleByEventStyle("*Default"u8, out var query, out var view);
        matched.Should().BeTrue();
        query.SequenceEqual("Default"u8).Should().BeTrue();
        view!.NameSpan.SequenceEqual("*Default"u8).Should().BeTrue();
    }

    [TestMethod]
    public void EventStyleMatch_StarDefault_DefaultStyleTwice()
    {
        var matched = stylesDefaultTwice.TryGetAssStyleByEventStyle("*Default"u8, out var query, out var view);
        matched.Should().BeTrue();
        query.SequenceEqual("Default"u8).Should().BeTrue();
        view!.NameSpan.SequenceEqual("Default"u8).Should().BeTrue();
    }

    [TestMethod]
    public void EventStyleMatch_default()
    {
        var matched = styles.TryGetAssStyleByEventStyle("default"u8, out var query, out var view);
        matched.Should().BeTrue();
        query.SequenceEqual("Default"u8).Should().BeTrue();
        view!.NameSpan.SequenceEqual("*Default"u8).Should().BeTrue();
    }

    [TestMethod]
    public void EventStyleMatch_default_WithoutDefaultStyle()
    {
        var matched = stylesWithoutDefault.TryGetAssStyleByEventStyle("default"u8, out var query, out var view);
        matched.Should().BeFalse();
        query.SequenceEqual("Default"u8).Should().BeTrue();
        view.LineRaw.IsEmpty.Should().BeTrue(); // Struct can't be null
    }

    private readonly AssStyles styles = BuildAssStyles("""
[Script Info]
Title: Default Aegisub file
ScriptType: v4.00+
WrapStyle: 0
ScaledBorderAndShadow: yes
PlayResX: 1280
PlayResY: 720
YCbCr Matrix: None

[V4+ Styles]
Format: Name, Fontname, Fontsize, PrimaryColour, SecondaryColour, OutlineColour, BackColour, Bold, Italic, Underline, StrikeOut, ScaleX, ScaleY, Spacing, Angle, BorderStyle, Outline, Shadow, Alignment, MarginL, MarginR, MarginV, Encoding
Style: *Default,HYQiHei 65S,56,&H00FFFFFF,&H000000FF,&H00000000,&H00000000,0,0,0,0,100,100,0,0,1,2,0,2,10,10,10,1
Style: default,HYXuanSong 65S,56,&H00FFFFFF,&H000000FF,&H00000000,&H00000000,0,0,0,0,100,100,0,0,1,2,0,2,10,10,10,1
Style: test,HYXuanSong 35S,56,&H00FFFFFF,&H000000FF,&H00000000,&H00000000,0,0,0,0,100,100,0,0,1,2,0,2,10,10,10,1
Style: *test,HYXuanSong 85S,56,&H00FFFFFF,&H000000FF,&H00000000,&H00000000,0,0,0,0,100,100,0,0,1,2,0,2,10,10,10,1

[Events]
Format: Layer, Start, End, Style, Name, MarginL, MarginR, MarginV, Effect, Text
Dialogue: 0,0:00:00.00,0:00:05.00,Default,,0,0,0,,12{\rdefault}2
"""u8);
    private readonly AssStyles stylesWithoutDefault = BuildAssStyles("""
[Script Info]
Title: Default Aegisub file
ScriptType: v4.00+
WrapStyle: 0
ScaledBorderAndShadow: yes
PlayResX: 1280
PlayResY: 720
YCbCr Matrix: None

[V4+ Styles]
Format: Name, Fontname, Fontsize, PrimaryColour, SecondaryColour, OutlineColour, BackColour, Bold, Italic, Underline, StrikeOut, ScaleX, ScaleY, Spacing, Angle, BorderStyle, Outline, Shadow, Alignment, MarginL, MarginR, MarginV, Encoding
Style: default,HYXuanSong 65S,56,&H00FFFFFF,&H000000FF,&H00000000,&H00000000,0,0,0,0,100,100,0,0,1,2,0,2,10,10,10,1
Style: test,HYXuanSong 35S,56,&H00FFFFFF,&H000000FF,&H00000000,&H00000000,0,0,0,0,100,100,0,0,1,2,0,2,10,10,10,1
Style: *test,HYXuanSong 85S,56,&H00FFFFFF,&H000000FF,&H00000000,&H00000000,0,0,0,0,100,100,0,0,1,2,0,2,10,10,10,1

[Events]
Format: Layer, Start, End, Style, Name, MarginL, MarginR, MarginV, Effect, Text
Dialogue: 0,0:00:00.00,0:00:05.00,Default,,0,0,0,,12{\rdefault}2
"""u8);

    private readonly AssStyles stylesDefaultTwice = BuildAssStyles("""
[Script Info]
Title: Default Aegisub file
ScriptType: v4.00+
WrapStyle: 0
ScaledBorderAndShadow: yes
PlayResX: 1280
PlayResY: 720
YCbCr Matrix: None

[V4+ Styles]
Format: Name, Fontname, Fontsize, PrimaryColour, SecondaryColour, OutlineColour, BackColour, Bold, Italic, Underline, StrikeOut, ScaleX, ScaleY, Spacing, Angle, BorderStyle, Outline, Shadow, Alignment, MarginL, MarginR, MarginV, Encoding
Style: *Default,HYQiHei 65S,56,&H00FFFFFF,&H000000FF,&H00000000,&H00000000,0,0,0,0,100,100,0,0,1,2,0,2,10,10,10,1
Style: Default,HYXuanSong 65S,56,&H00FFFFFF,&H000000FF,&H00000000,&H00000000,0,0,0,0,100,100,0,0,1,2,0,2,10,10,10,1
Style: test,HYXuanSong 35S,56,&H00FFFFFF,&H000000FF,&H00000000,&H00000000,0,0,0,0,100,100,0,0,1,2,0,2,10,10,10,1
Style: *test,HYXuanSong 85S,56,&H00FFFFFF,&H000000FF,&H00000000,&H00000000,0,0,0,0,100,100,0,0,1,2,0,2,10,10,10,1

[Events]
Format: Layer, Start, End, Style, Name, MarginL, MarginR, MarginV, Effect, Text
Dialogue: 0,0:00:00.00,0:00:05.00,Default,,0,0,0,,12{\rdefault}2
"""u8);

    private static AssStyles BuildAssStyles(ReadOnlySpan<byte> text)
    {
        var ass = new AssData();
        ass.ReadAssText(text);
        return ass.Styles;
    }
}