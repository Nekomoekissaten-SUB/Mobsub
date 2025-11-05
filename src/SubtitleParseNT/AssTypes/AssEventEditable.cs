using Microsoft.Extensions.Logging;
using ZLogger;
using System.Text;

namespace Mobsub.SubtitleParseNT2.AssTypes;

public sealed class AssEventEditable
{
    internal readonly ILogger? logger;

    public bool IsDialogue { get; set; }
    public int Layer { get; set; }
    public AssTime Start { get; set; }
    public AssTime End { get; set; }
    public string Style { get; set; }
    public string Name { get; set; }
    public int MarginL { get; set; }
    public int MarginR { get; set; }
    public int MarginV { get; set; }
    public string Effect { get; set; }
    public string Text { get; set; }

    public AssEventEditable(ILogger? logger)
    {
        this.logger = logger;
        IsDialogue = true;
        Layer = 0;
        Start = new(0);
        End = new(5000);
        Style = "Default";
        Name = string.Empty;
        MarginL = MarginR = MarginV = 0;
        Effect = string.Empty;
        Text = string.Empty;
    }
    public AssEventEditable(AssEventView view)
    {
        logger = view.logger;
        IsDialogue = view.IsDialogue;
        Layer = view.Layer;
        Start = view.Start;
        End = view.End;
        Style = view.GetStyle();
        Name = view.GetName();
        MarginL = view.MarginL;
        MarginR = view.MarginR;
        MarginV = view.MarginV;
        Effect = view.GetEffect();
        Text = view.GetText();
    }

    public void Write(StreamWriter sw, string[] fmts, bool ctsRounding)
    {
        var sb = new StringBuilder();
        Write(sb, fmts, ctsRounding);
        sw.Write(sb.ToString());
    }

    public void Write(StringBuilder sb, string[] fmts, bool ctsRounding)
    {
        sb.Append(IsDialogue ? "Dialogue: " : "Comment: ");

        for (var i = 0; i < fmts.Length; i++)
        {
            // var fmt = fmts[i];
            switch (fmts[i])
            {
                //case "Marked":
                //    sb.Append(Marked);
                //    break;
                case "Layer":
                    sb.Append(Layer);
                    break;
                case "Start":
                    AssTime.WriteAssTime(sb, Start, ctsRounding);
                    break;
                case "End":
                    AssTime.WriteAssTime(sb, End, ctsRounding);
                    break;
                case "Style":
                    sb.Append(Style);
                    break;
                case "Name":
                    sb.Append(Name);
                    break;
                case "MarginL":
                    sb.Append(MarginL);
                    break;
                case "MarginR":
                    sb.Append(MarginR);
                    break;
                case "MarginV":
                    sb.Append(MarginV);
                    break;
                //case "MarginT":
                //    sb.Append(MarginT);
                //    break;
                //case "MarginB":
                //    sb.Append(MarginB);
                //    break;
                case "Effect":
                    sb.Append(Effect);
                    break;
                case "Text":
                    sb.Append(Text);
                    break;
            }

            if (i < fmts.Length - 1)
            {
                sb.Append(',');
            }
        }
    }

    public override string ToString() => $"{(IsDialogue ? "Dialogue" : "Comment")}: {Layer},{Start},{End},{Style},{Name},{MarginL},{MarginR},{MarginV},{Effect},{Text}";
}
