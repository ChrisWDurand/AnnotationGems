using System.Windows;
using AnnotationGems.Core.Annotations;
using AnnotationGems.Rendering;

namespace AnnotationGems.Interaction.Commands;

public sealed class MoveBoxesCommand : IUndoableCommand
{
    private readonly AnnotationOverlay _overlay;
    private readonly Dictionary<BoundingBox, Rect> _before;
    private readonly Dictionary<BoundingBox, Rect> _after;

    public string Name => "Move Boxes";

    public MoveBoxesCommand(
        AnnotationOverlay overlay,
        Dictionary<BoundingBox, Rect> before,
        Dictionary<BoundingBox, Rect> after)
    {
        _overlay = overlay;
        _before = before;
        _after = after;
    }

    public void Do()
    {
        foreach (var kv in _after)
            kv.Key.SetFromRect(kv.Value);

        _overlay.Refresh();
    }

    public void Undo()
    {
        foreach (var kv in _before)
            kv.Key.SetFromRect(kv.Value);

        _overlay.Refresh();
    }
}

