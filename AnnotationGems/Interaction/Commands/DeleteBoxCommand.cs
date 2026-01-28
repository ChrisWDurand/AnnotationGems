using AnnotationGems.Core.Annotations;
using AnnotationGems.Rendering;

namespace AnnotationGems.Interaction.Commands;

public sealed class DeleteBoxesCommand : IUndoableCommand
{
    private readonly AnnotationOverlay _overlay;
    private readonly List<BoundingBox> _boxes;
    private readonly List<int> _indices = new();

    public string Name => "Delete Boxes";

    public DeleteBoxesCommand(AnnotationOverlay overlay, List<BoundingBox> boxes)
    {
        _overlay = overlay;
        _boxes = boxes;
    }

    public void Do()
    {
        _indices.Clear();

        // Record indices before removal
        foreach (var b in _boxes)
            _indices.Add(_overlay.Annotations.IndexOf(b));

        // Remove in descending index order to keep indices valid
        var pairs = _boxes.Zip(_indices, (b, i) => (b, i))
                          .Where(p => p.i >= 0)
                          .OrderByDescending(p => p.i)
                          .ToList();

        foreach (var (b, i) in pairs)
            _overlay.Annotations.RemoveAt(i);

        _overlay.ClearSelection();
        _overlay.Refresh();
    }

    public void Undo()
    {
        // Reinsert in ascending order
        var pairs = _boxes.Zip(_indices, (b, i) => (b, i))
                          .Where(p => p.i >= 0)
                          .OrderBy(p => p.i)
                          .ToList();

        foreach (var (b, i) in pairs)
        {
            var idx = Math.Min(i, _overlay.Annotations.Count);
            _overlay.Annotations.Insert(idx, b);
            _overlay.Selected.Add(b);
        }

        _overlay.Refresh();
    }
}
