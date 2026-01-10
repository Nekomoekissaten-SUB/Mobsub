using System.Text;
using FluentAssertions;
using Mobsub.SubtitleParseNT2.AssTypes;

namespace Mobsub.Test;

[TestClass]
public class AssDataEncodingTest
{
    private const string SampleAss =
        "[Script Info]\r\n" +
        "Title: UTF16\r\n" +
        "ScriptType: v4.00+\r\n" +
        "\r\n" +
        "[V4+ Styles]\r\n" +
        "Format: Name, Fontname, Fontsize, PrimaryColour, SecondaryColour, OutlineColour, BackColour, Bold, Italic, Underline, StrikeOut, ScaleX, ScaleY, Spacing, Angle, BorderStyle, Outline, Shadow, Alignment, MarginL, MarginR, MarginV, Encoding\r\n" +
        "Style: Default,Arial,20,&H00FFFFFF,&H000000FF,&H00000000,&H00000000,0,0,0,0,100,100,0,0,1,2,2,2,10,10,10,1\r\n" +
        "\r\n" +
        "[Events]\r\n" +
        "Format: Layer, Start, End, Style, Name, MarginL, MarginR, MarginV, Effect, Text\r\n" +
        "Dialogue: 0,0:00:01.00,0:00:02.00,Default,,0,0,0,,Hello UTF16\r\n";

    [TestMethod]
    public void ReadAssText_HandlesUtf16LeBom()
    {
        var data = WithBom(Encoding.Unicode);
        var ass = new AssData().ReadAssText(data);

        ass.ScriptInfo.Title.Should().Be("UTF16");
        ass.Events.Should().NotBeNull();
        ass.Events!.Collection.Count.Should().Be(1);
        ass.Events.Collection[0].Text.Should().Be("Hello UTF16");
    }

    [TestMethod]
    public void ReadAssText_HandlesUtf16BeBom()
    {
        var data = WithBom(Encoding.BigEndianUnicode);
        var ass = new AssData().ReadAssText(data);

        ass.ScriptInfo.Title.Should().Be("UTF16");
        ass.Events.Should().NotBeNull();
        ass.Events!.Collection.Count.Should().Be(1);
        ass.Events.Collection[0].Text.Should().Be("Hello UTF16");
    }

    [TestMethod]
    public void ReadAssText_UsesExplicitEncodingWithoutBom()
    {
        var data = Encoding.Latin1.GetBytes(BuildSample("Cafe\u00E9", "Bonjour Caf\u00E9"));
        var ass = new AssData().ReadAssText(data, Encoding.Latin1);

        ass.ScriptInfo.Title.Should().Be("Cafe\u00E9");
        ass.Events.Should().NotBeNull();
        ass.Events!.Collection.Count.Should().Be(1);
        ass.Events.Collection[0].Text.Should().Be("Bonjour Caf\u00E9");
    }

    [TestMethod]
    public void ReadAssText_UsesDetectorWhenNoBom()
    {
        var data = Encoding.Latin1.GetBytes(BuildSample("Cafe\u00E9", "Bonjour Caf\u00E9"));
        var ass = new AssData().ReadAssText(data, _ => Encoding.Latin1);

        ass.ScriptInfo.Title.Should().Be("Cafe\u00E9");
        ass.Events.Should().NotBeNull();
        ass.Events!.Collection.Count.Should().Be(1);
        ass.Events.Collection[0].Text.Should().Be("Bonjour Caf\u00E9");
    }

    private static string BuildSample(string title, string text)
    {
        return
            "[Script Info]\r\n" +
            $"Title: {title}\r\n" +
            "ScriptType: v4.00+\r\n" +
            "\r\n" +
            "[V4+ Styles]\r\n" +
            "Format: Name, Fontname, Fontsize, PrimaryColour, SecondaryColour, OutlineColour, BackColour, Bold, Italic, Underline, StrikeOut, ScaleX, ScaleY, Spacing, Angle, BorderStyle, Outline, Shadow, Alignment, MarginL, MarginR, MarginV, Encoding\r\n" +
            "Style: Default,Arial,20,&H00FFFFFF,&H000000FF,&H00000000,&H00000000,0,0,0,0,100,100,0,0,1,2,2,2,10,10,10,1\r\n" +
            "\r\n" +
            "[Events]\r\n" +
            "Format: Layer, Start, End, Style, Name, MarginL, MarginR, MarginV, Effect, Text\r\n" +
            $"Dialogue: 0,0:00:01.00,0:00:02.00,Default,,0,0,0,,{text}\r\n";
    }

    private static byte[] WithBom(Encoding encoding)
    {
        var preamble = encoding.GetPreamble();
        var payload = encoding.GetBytes(SampleAss);
        var data = new byte[preamble.Length + payload.Length];
        Buffer.BlockCopy(preamble, 0, data, 0, preamble.Length);
        Buffer.BlockCopy(payload, 0, data, preamble.Length, payload.Length);
        return data;
    }
}
