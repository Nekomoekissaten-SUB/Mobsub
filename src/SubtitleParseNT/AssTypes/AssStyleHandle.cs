namespace Mobsub.SubtitleParseNT2.AssTypes;

public class AssStyleHandle(AssStyleView view)
{
    private object _style = view;

    public bool IsEditing() => _style is not AssStyleView;
}
