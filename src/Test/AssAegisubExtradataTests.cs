using System.Text;
using FluentAssertions;
using Mobsub.SubtitleParse.AssTypes;

namespace Mobsub.Test;

[TestClass]
public sealed class AssAegisubExtradataTests
{
    [TestMethod]
    public void ParseAndWrite_AegisubExtradata_StripsMarkerAndRoundTrips()
    {
        const string script =
            "[Script Info]\n" +
            "ScriptType: v4.00+\n" +
            "\n" +
            "[V4+ Styles]\n" +
            "Format: Name, Fontname, Fontsize, PrimaryColour, SecondaryColour, OutlineColour, BackColour, Bold, Italic, Underline, StrikeOut, ScaleX, ScaleY, Spacing, Angle, BorderStyle, Outline, Shadow, Alignment, MarginL, MarginR, MarginV, Encoding\n" +
            "Style: Default,Arial,20,&H00FFFFFF,&H000000FF,&H00000000,&H00000000,0,0,0,0,100,100,0,0,1,2,0,2,10,10,10,1\n" +
            "\n" +
            "[Events]\n" +
            "Format: Layer, Start, End, Style, Name, MarginL, MarginR, MarginV, Effect, Text\n" +
            "Dialogue: 0,0:00:00.00,0:00:01.00,Default,,0,0,0,,{=1=2}Hello\n" +
            "\n" +
            "[Aegisub Extradata]\n" +
            "Data: 1,My#3AKey,eValue#2COne\n" +
            "Data: 2,Key2,eSecond\n";

        var encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
        var raw = encoding.GetBytes(script);

        var a1 = new AssData();
        a1.ExtractAegisubExtradataMarkers = true;
        a1.ReadAssBytes(raw, encoding);

        a1.Events.Should().NotBeNull();
        a1.Events!.Collection.Count.Should().BeGreaterThan(0);

        var evt1 = a1.Events.Collection[0];
        evt1.Text.Should().Be("Hello");
        evt1.AegisubExtradataIds.Should().NotBeNull();
        evt1.AegisubExtradataIds!.Should().Equal([1u, 2u]);

        a1.Extradata.Entries.Count.Should().Be(2);
        a1.Extradata.Entries[1].Key.Should().Be("My:Key");
        a1.Extradata.Entries[1].Value.Should().Be("Value,One");

        var tmp = Path.GetTempFileName();
        try
        {
            a1.CharEncoding = encoding;
            a1.WriteAssFile(tmp, forceEnv: false, ctsRounding: false);

            var raw2 = File.ReadAllBytes(tmp);
            var a2 = new AssData();
            a2.ExtractAegisubExtradataMarkers = true;
            a2.ReadAssBytes(raw2, encoding);

            a2.Events.Should().NotBeNull();
            a2.Events!.Collection.Count.Should().BeGreaterThan(0);

            var evt2 = a2.Events.Collection[0];
            evt2.Text.Should().Be("Hello");
            evt2.AegisubExtradataIds.Should().NotBeNull();
            evt2.AegisubExtradataIds!.Should().Equal([1u, 2u]);

            a2.Extradata.Entries.Count.Should().Be(2);
            a2.Extradata.Entries[1].Key.Should().Be("My:Key");
            a2.Extradata.Entries[1].Value.Should().Be("Value,One");
        }
        finally
        {
            try { File.Delete(tmp); } catch { }
        }
    }
}
