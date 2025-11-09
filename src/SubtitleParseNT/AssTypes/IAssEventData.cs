using System;
using System.Collections.Generic;
using System.Text;

namespace Mobsub.SubtitleParseNT2.AssTypes;

public interface IAssEventData
{
    public bool IsDialogue { get; }
    public int Layer { get; }
    public int Marked { get; }
    public AssTime Start { get; }
    public AssTime End { get; }
    public string Style { get; }
    public string Name { get; }
    public int MarginL { get; }
    public int MarginR { get; }
    public int MarginV { get; }
    public int MarginT { get; }
    public int MarginB { get; }
    public string Effect { get; }
    public string Text { get; }
}
