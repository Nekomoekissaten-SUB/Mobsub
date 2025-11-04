using System;
using System.Collections.Generic;
using System.Text;

namespace Mobsub.SubtitleParseNT2.AssTypes;

public class AssEventHandle(AssEventView view)
{
    private object _event = view;

    public bool IsEditing() => _event is not AssEventView;
}
