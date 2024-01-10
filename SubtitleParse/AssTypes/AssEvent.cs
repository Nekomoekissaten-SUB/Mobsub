using System.Text;

namespace Mobsub.AssTypes;

public class AssEvents
{
    private readonly string formatV4P = "Layer, Start, End, Style, Name, MarginL, MarginR, MarginV, Effect, Text";
    // private readonly string formatV4PP = "Layer, Start, End, Style, Name, MarginL, MarginR, MarginT, MarginB, Effect, Text";
    // private readonly string formatV4 = "Marked, Start, End, Style, Name, MarginL, MarginR, MarginV, Effect, Text";
    private string[]? formats;
    public string[] Formats
    {
        get => formats ?? formatV4P.Split(',').Select(s => s.Trim()).ToArray();
        set => formats = value;
    }
    public List<AssEvent> Collection = [];

    public void Write(StreamWriter sw, char[] newline, bool ctsRounding)
    {
        sw.Write("[Events]");
        sw.Write(newline);
        sw.Write($"Format: {string.Join(", ", Formats)}");
        sw.Write(newline);

        for (var i = 0; i < Collection.Count; i++)
        {
            Collection[i].Write(sw, Formats, ctsRounding);
            sw.Write(newline);
        }
    }
}

public class AssEvent
{
    private int layer = 0;
    public int lineNumber;
    public bool StartSemicolon = false;
    public string? Untouched = string.Empty;
    public bool IsDialogue = true;
    public int Layer
    {
        get => layer;
        set => layer = value >= 0 ? value : 0;
    }
    public readonly int Marked = 0;
    public AssTime Start;
    public AssTime End;
    public string Style = "Default";
    public string Name = string.Empty;
    public int MarginL = 0;
    public int MarginR = 0;
    public int MarginV = 0;
    public int MarginT = 0;
    public int MarginB = 0;
    public string? Effect;
    public List<char[]> Text = [];  // override tags block, special chars block, normal text block

    public static void WriteTime(StreamWriter sw, AssTime time, bool ctsRounding)
    {
        sw.Write(time.Hour);
        sw.Write(':');
        WriteChar(sw, time.Minute, 2);
        sw.Write(':');
        WriteChar(sw, time.Second, 2);
        sw.Write('.');

        if (ctsRounding)
        {
            WriteChar(sw, DigitRounding(time.Millisecond), 3);
        }
        else
        {
            WriteChar(sw, time.Millisecond, 3);
        }
        
    }

    private static void WriteChar(StreamWriter sw, int val, int length)
    {
        var ca = new char[length];
        
        var divisor = 1;
        for (var i = 1; i < length; i++)
        {
            divisor *= 10;
        }

        for (int i = 0; i < length; i++)
        {
            ca[i] = (char)(val / divisor + '0');
            val %= divisor;
            divisor /= 10;
        }

        sw.Write(ca[0..Math.Min(length, 2)]);
    }

    private static int DigitRounding(int i)
    {
        var last = i % 10;
        return (i > 994) ? 990 : last >= 0 && last <= 4 ? i - last : i + (10 - last);
    }

    public void Write(StreamWriter sw, string[] fmts, bool ctsRounding)
    {
        if (StartSemicolon)
        {
            sw.Write($";{Untouched}");
        }
        else
        {
            sw.Write(IsDialogue ? "Dialogue: " : "Comment: ");
            
            for (var i = 0; i < fmts.Length; i++)
            {
                // var fmt = fmts[i];
                switch (fmts[i])
                {
                    case "Marked":
                        sw.Write(Marked);
                        break;
                    case "Layer":
                        sw.Write(Layer);
                        break;
                    case "Start":
                        WriteTime(sw, Start, ctsRounding);
                        break;
                    case "End":
                        WriteTime(sw, End, ctsRounding);
                        break;
                    case "Style":
                        sw.Write(Style);
                        break;
                    case "Name":
                        sw.Write(Name);
                        break;
                    case "MarginL":
                        sw.Write(MarginL);
                        break;
                    case "MarginR":
                        sw.Write(MarginR);
                        break;
                    case "MarginV":
                        sw.Write(MarginV);
                        break;
                    case "MarginT":
                        sw.Write(MarginT);
                        break;
                    case "MarginB":
                        sw.Write(MarginB);
                        break;
                    case "Effect":
                        sw.Write(Effect);
                        break;
                    case "Text":
                        foreach (var ca in Text)
                        {
                            sw.Write(ca);
                        }
                        break;
                }
                
                if (i < fmts.Length - 1)
                {
                    sw.Write(',');
                }
            }
        }
    }

}
