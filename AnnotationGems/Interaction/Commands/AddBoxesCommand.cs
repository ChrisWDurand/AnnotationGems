using AnnotationGems.Core.Annotations;
using AnnotationGems.Rendering;

namespace AnnotationGems.Interaction.Commands;

public sealed class AddBoxesCommand : IUndoableCommand
{
    private readonly AnnotationOverlay _overlay;
    private readonly List<BoundingBox> _boxes;

    public string Name => "Add Boxes";

    public AddBoxesCommand(AnnotationOverlay overlay, List<BoundingBox> boxes)
    {
        _overlay = overlay;
        _boxes = boxes;
    }

    public void Do()
    {
        foreach (var b in _boxes)
            _overlay.Annotations.Add(b);

        _overlay.Refresh();
    }

    public void Undo()
    {
        foreach (var b in _boxes)
            _overlay.Annotations.Remove(b);

        // Remove from selection too
        foreach (var b in _boxes)
            _overlay.Selected.Remove(b);

        _overlay.Refresh();
    }
}
