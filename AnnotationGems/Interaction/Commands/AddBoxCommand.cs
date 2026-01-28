using AnnotationGems.Core.Annotations;
using AnnotationGems.Rendering;

namespace AnnotationGems.Interaction.Commands;

public sealed class AddBoxCommand : IUndoableCommand
{
    private readonly AnnotationOverlay _overlay;
    private readonly BoundingBox _box;
    private readonly int _index;

    public string Name => "Add Box";

    public AddBoxCommand(AnnotationOverlay overlay, BoundingBox box, int index = -1)
    {
        _overlay = overlay;
        _box = box;
        _index = index;
    }

    public void Do()
    {
        if (_index >= 0 && _index <= _overlay.Annotations.Count)
            _overlay.Annotations.Insert(_index, _box);
        else
            _overlay.Annotations.Add(_box);

        _overlay.ClearSelection();
        _overlay.Selected.Add(_box);
        _overlay.Refresh();
    }

    public void Undo()
    {
        _overlay.Annotations.Remove(_box);
        _overlay.Selected.Remove(_box);
        _overlay.Refresh();
    }
}
