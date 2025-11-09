namespace Mobsub.SubtitleParseNT2.AssTypes;

public class AssStyleHandle(AssStyleView view)
{
    private AssStyleView _view = view;
    private AssStyleEditable? _editable;
    private AssHandleState _state = AssHandleState.ViewOriginal;

    public bool IsEditing => _state == AssHandleState.Editable;
    public bool IsModified => _state == AssHandleState.ViewModified;

    public AssStyleEditable? GetEditable() => _editable;

    public void ModifyValue(Action<AssStyleView> modifier)
    {
        modifier(_view);
        _state = AssHandleState.ViewModified;
    }
    public void BeginEdit()
    {
        _editable ??= new AssStyleEditable(_view);
        _state = AssHandleState.Editable;
    }
    public void Write(TextWriter writer, string[] formats)
    {
        switch (_state)
        {
            case AssHandleState.ViewOriginal:
                writer.WriteLine(Utils.GetString(_view.LineRaw));
                break;

            case AssHandleState.ViewModified:
                Helper.Write(writer, _view, formats);
                break;

            case AssHandleState.Editable:
                Helper.Write(writer, _editable!, formats);
                break;
        }
    }
}
