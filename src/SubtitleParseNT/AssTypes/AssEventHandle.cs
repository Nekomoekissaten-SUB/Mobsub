namespace Mobsub.SubtitleParseNT2.AssTypes;

public class AssEventHandle(AssEventView view)
{
    private AssEventView _view = view;
    private AssEventEditable? _editable;
    private AssHandleState _state = AssHandleState.ViewOriginal;

    public bool IsEditing => _state == AssHandleState.Editable;
    public bool IsModified => _state == AssHandleState.ViewModified;

    public AssEventEditable? GetEditable() => _editable;

    public void ModifyValue(Action<AssEventView> modifier)
    {
        modifier(_view);
        _state = AssHandleState.ViewModified;
    }
    public void BeginEdit()
    {
        _editable ??= new AssEventEditable(_view);
        _state = AssHandleState.Editable;
    }
    public void Write(TextWriter writer, string[] formats, bool ctsRounding)
    {
        switch (_state)
        {
            case AssHandleState.ViewOriginal:
                writer.WriteLine(Utils.GetString(_view.LineRaw));
                break;

            case AssHandleState.ViewModified:
                Helper.Write(writer, _view, formats, ctsRounding);
                break;

            case AssHandleState.Editable:
                Helper.Write(writer, _editable!, formats, ctsRounding);
                break;
        }
    }
}
