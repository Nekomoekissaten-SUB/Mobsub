﻿namespace Mobsub.SubtitleParse.AssTypes;

public static class Helper
{
    public static void Write(TextWriter writer, AssStyle style, string[] formats)
    {
        writer.Write("Style: ");
        for (int i = 0; i < formats.Length; i++)
        {
            switch (formats[i])
            {
                case "Name":
                    writer.Write(style.Name);
                    break;
                case "Fontname":
                    writer.Write(style.Fontname);
                    break;
                case "Fontsize":
                    writer.Write(style.Fontsize);
                    break;
                case "PrimaryColour":
                    writer.Write("&H");
                    writer.Write(style.PrimaryColour.ConvertToString(true));
                    break;
                case "SecondaryColour":
                    writer.Write("&H");
                    writer.Write(style.SecondaryColour.ConvertToString(true));
                    break;
                case "OutlineColour":
                    writer.Write("&H");
                    writer.Write(style.OutlineColour.ConvertToString(true));
                    break;
                case "BackColour":
                    writer.Write("&H");
                    writer.Write(style.BackColour.ConvertToString(true));
                    break;
                case "Bold":
                    writer.Write(style.Bold ? -1 : 0);
                    break;
                case "Italic":
                    writer.Write(style.Italic ? -1 : 0);
                    break;
                case "Underline":
                    writer.Write(style.Underline ? -1 : 0);
                    break;
                case "StrikeOut":
                    writer.Write(style.StrikeOut ? -1 : 0);
                    break;
                case "ScaleX":
                    writer.Write(style.ScaleX);
                    break;
                case "ScaleY":
                    writer.Write(style.ScaleY);
                    break;
                case "Spacing":
                    writer.Write(style.Spacing);
                    break;
                case "Angle":
                    writer.Write(style.Angle);
                    break;
                case "BorderStyle":
                    writer.Write(style.BorderStyle);
                    break;
                case "Outline":
                    writer.Write(style.Outline);
                    break;
                case "Shadow":
                    writer.Write(style.Shadow);
                    break;
                case "Alignment":
                    writer.Write(style.Alignment);
                    break;
                case "MarginL":
                    writer.Write(style.MarginL);
                    break;
                case "MarginR":
                    writer.Write(style.MarginR);
                    break;
                case "MarginV":
                    writer.Write(style.MarginV);
                    break;
                case "Encoding":
                    writer.Write(style.Encoding);
                    break;
            }

            if (i < formats.Length - 1)
                writer.Write(',');
        }
    }
    public static void Write(TextWriter writer, AssEvent evt, string[] fmts, bool ctsRounding)
    {
        writer.Write(evt.IsDialogue ? "Dialogue: " : "Comment: ");

        for (var i = 0; i < fmts.Length; i++)
        {
            // var fmt = fmts[i];
            switch (fmts[i])
            {
                case "Marked":
                    writer.Write(evt.Marked);
                    break;
                case "Layer":
                    writer.Write(evt.Layer);
                    break;
                case "Start":
                    AssTime.WriteAssTime(writer, evt.Start, ctsRounding);
                    break;
                case "End":
                    AssTime.WriteAssTime(writer, evt.End, ctsRounding);
                    break;
                case "Style":
                    writer.Write(evt.Style);
                    break;
                case "Name":
                    writer.Write(evt.Name);
                    break;
                case "MarginL":
                    writer.Write(evt.MarginL);
                    break;
                case "MarginR":
                    writer.Write(evt.MarginR);
                    break;
                case "MarginV":
                    writer.Write(evt.MarginV);
                    break;
                case "MarginT":
                    writer.Write(evt.MarginT);
                    break;
                case "MarginB":
                    writer.Write(evt.MarginB);
                    break;
                case "Effect":
                    writer.Write(evt.Effect);
                    break;
                case "Text":
                    writer.Write(evt.Text);
                    break;
            }

            if (i < fmts.Length - 1)
            {
                writer.Write(',');
            }
        }
    }
}

