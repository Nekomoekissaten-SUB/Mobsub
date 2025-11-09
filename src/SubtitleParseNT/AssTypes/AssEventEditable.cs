using Microsoft.Extensions.Logging;
using ZLogger;
using System.Text;

namespace Mobsub.SubtitleParseNT2.AssTypes;

public sealed class AssEventEditable : IAssEventData
{
    internal readonly ILogger? logger;

    public bool IsDialogue { get; set; }
    public int Layer { get; set; }
    public int Marked { get; set; }
    public AssTime Start { get; set; }
    public AssTime End { get; set; }
    public string Style { get; set; }
    public string Name { get; set; }
    public int MarginL { get; set; }
    public int MarginR { get; set; }
    public int MarginV { get; set; }
    public int MarginT { get; set; }
    public int MarginB { get; set; }
    public string Effect { get; set; }
    public string Text { get; set; }

    public AssEventEditable(ILogger? logger = null)
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
        Style = view.Style;
        Name = view.Name;
        MarginL = view.MarginL;
        MarginR = view.MarginR;
        MarginV = view.MarginV;
        Effect = view.Effect;
        Text = view.Text;
    }

    public override string ToString() => $"{(IsDialogue ? "Dialogue" : "Comment")}: {Layer},{Start},{End},{Style},{Name},{MarginL},{MarginR},{MarginV},{Effect},{Text}";
}
