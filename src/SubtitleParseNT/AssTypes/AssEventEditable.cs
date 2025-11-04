using System;
using System.Collections.Generic;
using System.Text;

namespace Mobsub.SubtitleParseNT2.AssTypes;

public sealed class AssEventEditable(AssEventView view)
{
    public bool IsDialogue { get; set; } = view.IsDialogue;
    public int Layer { get; set; } = view.Layer;
    public AssTime Start { get; set; } = view.Start;
    public AssTime End { get; set; } = view.End;
    public string Style { get; set; } = view.GetStyle();
    public string Name { get; set; } = view.GetName();
    public int MarginL { get; set; } = view.MarginL;
    public int MarginR { get; set; } = view.MarginB;
    public int MarginV { get; set; } = view.MarginV;
    public string Effect { get; set; } = view.GetEffect();
    public string Text { get; set; } = view.GetText();

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
