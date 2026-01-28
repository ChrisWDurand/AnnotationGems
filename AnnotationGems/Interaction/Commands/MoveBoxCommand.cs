using System.Windows;
using AnnotationGems.Core.Annotations;
using AnnotationGems.Rendering;

namespace AnnotationGems.Interaction.Commands;
public sealed class MoveBoxCommand : IUndoableCommand
{
    private readonly AnnotationOverlay _overlay;
    private readonly BoundingBox _box;
    private readonly Rect _before;
    private readonly Rect _after;

    public string Name => "Move Box";

    public MoveBoxCommand(AnnotationOverlay overlay, BoundingBox box, Rect before, Rect after)
    {
        _overlay = overlay;
        _box = box;
        _before = before;
        _after = after;
    }

    public void Do()
    {
        _box.SetFromRect(_after);
        _overlay.Refresh();
    }

    public void Undo()
    {
        _box.SetFromRect(_before);
        _overlay.Refresh();
    }
}
