using System.Windows;
using System.Windows.Input;
using AnnotationGems.Core.Annotations;
using AnnotationGems.Rendering;
using AnnotationGems.Interaction.Commands;

namespace AnnotationGems.Interaction;

public sealed class InteractionController
{
    private readonly AnnotationOverlay _overlay;

    // History (undo/redo)
    private readonly UndoRedoStack _history = new();

    // IDs
    private int _nextId = 1;

    // Mouse state
    private Point _lastMouseScreen;
    private bool _isPanning;

    // Create box (right-drag)
    private bool _isCreatingBox;
    private Point _createAnchorImg;

    // Marquee selection (left-drag on empty)
    private bool _isMarqueeSelecting;
    private Point _marqueeAnchorImg;

    // Drag selected group (left-drag on selected)
    private bool _isDraggingGroup;
    private Point _groupDragAnchorImg;
    private readonly Dictionary<BoundingBox, Rect> _groupDragStart = new();

    // Copy/paste buffers
    private List<BoundingBox> _copiedBoxes = new();
    private Size? _lastCreatedBoxSize;

    // Resize modals
    private bool _isResizing;
    private AnnotationGems.Rendering.ResizeHandle _activeHandle = AnnotationGems.Rendering.ResizeHandle.None;
    private BoundingBox? _resizeBox;
    private Rect _resizeStartRect;

    // Categoricals
    public int ActiveCategoryId { get; set; } = 1;
    public string ActiveCategoryName { get; set; } = "default";

    // Coordinating scroll/mouse-wheel action
    private readonly Action? _onViewportChanged;

    public InteractionController(AnnotationOverlay overlay, Action? onViewportChanged = null)
    {
        _overlay = overlay;
        _onViewportChanged = onViewportChanged;
        _overlay.Focusable = true;
        WireEvents();
    }


    private BoundingBox? FindContainingParent(Rect childRect, int childCategoryId)
    {
        // Find parents that fully contain childRect, pick the smallest-area parent (most specific)
        BoundingBox? best = null;
        double bestArea = double.MaxValue;

        foreach (var ann in _overlay.Annotations)
        {
            if (ann is not BoundingBox parent) continue;

            // later: check rule CanContain(parent.CategoryId, childCategoryId)
            // for now, with one class, allow containment or skip based on your preference

            var pr = parent.ToRect();
            if (pr.Contains(childRect))
            {
                var area = pr.Width * pr.Height;
                if (area < bestArea)
                {
                    bestArea = area;
                    best = parent;
                }
            }
        }

        return best;
    }

    private static Rect ResizeRectFromHandle(Rect start, Point cursor, ResizeHandle handle)
    {
        const double minSize = 1.0;

        double left = start.Left;
        double right = start.Right;
        double top = start.Top;
        double bottom = start.Bottom;

        // Horizontal adjustments
        switch (handle)
        {
            case ResizeHandle.W:
            case ResizeHandle.NW:
            case ResizeHandle.SW:
                // Drag left edge, clamp so it can't cross right-min
                left = Math.Min(cursor.X, right - minSize);
                break;

            case ResizeHandle.E:
            case ResizeHandle.NE:
            case ResizeHandle.SE:
                // Drag right edge, clamp so it can't cross left+min
                right = Math.Max(cursor.X, left + minSize);
                break;

                // N/S: no horizontal change
        }

        // Vertical adjustments
        switch (handle)
        {
            case ResizeHandle.N:
            case ResizeHandle.NW:
            case ResizeHandle.NE:
                // Drag top edge, clamp so it can't cross bottom-min
                top = Math.Min(cursor.Y, bottom - minSize);
                break;

            case ResizeHandle.S:
            case ResizeHandle.SW:
            case ResizeHandle.SE:
                // Drag bottom edge, clamp so it can't cross top+min
                bottom = Math.Max(cursor.Y, top + minSize);
                break;

                // W/E: no vertical change
        }

        return new Rect(new Point(left, top), new Point(right, bottom));
    }



    private void WireEvents()
    {
        _overlay.MouseDown += OnMouseDown;
        _overlay.MouseUp += OnMouseUp;
        _overlay.MouseMove += OnMouseMove;
        _overlay.MouseWheel += OnMouseWheel;

        _overlay.KeyDown += OnKeyDown;
    }

    private void OnMouseDown(object sender, MouseButtonEventArgs e)
    {
        _overlay.Focus();
        _lastMouseScreen = e.GetPosition(_overlay);

        if (e.ChangedButton == MouseButton.Middle)
        {
            _isPanning = true;
            _overlay.CaptureMouse();
            e.Handled = true;
            return;
        }

        if (e.ChangedButton == MouseButton.Right)
        {
            var selectedBoxes = _overlay.Selected.OfType<BoundingBox>().ToList();
            var mouseImg = _overlay.Viewport.ScreenToImage(_lastMouseScreen);

            // If multiple selected: clear selection and immediately start creating a new box
            if (selectedBoxes.Count > 1)
            {
                _overlay.ClearSelection();
                _overlay.Refresh();

                _isCreatingBox = true;
                _createAnchorImg = mouseImg;
                _overlay.PreviewBoxImageSpace = new Rect(_createAnchorImg, _createAnchorImg);
                _overlay.CaptureMouse();
                e.Handled = true;
                return;
            }

            // If exactly one selected and on a handle: resize
            if (selectedBoxes.Count == 1)
            {
                var single = selectedBoxes[0];
                var handle = _overlay.HitTestHandle(_lastMouseScreen, single);

                if (handle != ResizeHandle.None)
                {
                    _isResizing = true;
                    _activeHandle = handle;
                    _resizeBox = single;
                    _resizeStartRect = single.ToRect();  // resize based on start rect

                    _overlay.CaptureMouse();
                    e.Handled = true;
                    return;
                }
            }

            // Otherwise: start creating a new box (right-click drag)
            _isCreatingBox = true;
            _createAnchorImg = mouseImg;
            _overlay.PreviewBoxImageSpace = new Rect(_createAnchorImg, _createAnchorImg);
            _overlay.CaptureMouse();
            _overlay.Refresh();
            e.Handled = true;
            return;
        }


        if (e.ChangedButton == MouseButton.Left)
        {
            var hit = _overlay.HitTestBox(_lastMouseScreen);

            var ctrl = (Keyboard.Modifiers & ModifierKeys.Control) != 0;
            var shift = (Keyboard.Modifiers & ModifierKeys.Shift) != 0;

            // Empty space => start marquee selection
            if (hit is null)
            {
                _isMarqueeSelecting = true;
                _marqueeAnchorImg = _overlay.Viewport.ScreenToImage(_lastMouseScreen);
                _overlay.SelectionMarqueeImageSpace = new Rect(_marqueeAnchorImg, _marqueeAnchorImg);

                if (!ctrl)
                    _overlay.ClearSelection();

                _overlay.CaptureMouse();
                _overlay.Refresh();
                e.Handled = true;
                return;
            }

            // If clicking an already-selected box with no modifiers, start drag immediately
            // and do NOT change the selection.
            if (!ctrl && !shift && _overlay.Selected.Contains(hit))
            {
                BeginGroupDrag(_lastMouseScreen);
                e.Handled = true;
                return;
            }

            // Otherwise, update selection rules
            if (ctrl)
            {
                if (_overlay.Selected.Contains(hit))
                    _overlay.Selected.Remove(hit);
                else
                    _overlay.Selected.Add(hit);
            }
            else if (shift)
            {
                _overlay.Selected.Add(hit);
            }
            else
            {
                _overlay.ClearSelection();
                _overlay.Selected.Add(hit);
            }

            _overlay.Refresh();

            // Start drag if clicked box is selected after selection rules
            if (_overlay.Selected.Contains(hit))
            {
                BeginGroupDrag(_lastMouseScreen);
                e.Handled = true;
                return;
            }

            e.Handled = true;
            return;
        }
    }

    private void OnMouseMove(object sender, MouseEventArgs e)
    {
        var pScreen = e.GetPosition(_overlay);
        var deltaScreen = pScreen - _lastMouseScreen;

        // Pan (middle drag)
        if (_isPanning && e.MiddleButton == MouseButtonState.Pressed)
        {
            _overlay.Viewport.PanBy(deltaScreen);
            _lastMouseScreen = pScreen;
            _onViewportChanged?.Invoke();
            _overlay.Refresh();
            return;
        }

        if (_isResizing && _resizeBox is not null && e.RightButton == MouseButtonState.Pressed)
        {
            var curImg = _overlay.Viewport.ScreenToImage(pScreen);

            // compute from _resizeStartRect, not from current box rect
            var newRect = ResizeRectFromHandle(_resizeStartRect, curImg, _activeHandle);

            newRect = ClampRectToImage(newRect);
            _resizeBox.SetFromRect(newRect);

            _overlay.Refresh();
            return;
        }



        // Create box (right drag)
        if (_isCreatingBox && e.RightButton == MouseButtonState.Pressed)
        {
            var curImg = _overlay.Viewport.ScreenToImage(pScreen);
            _overlay.PreviewBoxImageSpace = NormalizeRect(_createAnchorImg, curImg);
            _overlay.Refresh();
            return;
        }

        // Drag selected group (left drag)
        if (_isDraggingGroup && e.LeftButton == MouseButtonState.Pressed)
        {
            var curImg = _overlay.Viewport.ScreenToImage(pScreen);
            var deltaImg = curImg - _groupDragAnchorImg;

            foreach (var kv in _groupDragStart)
            {
                var b = kv.Key;
                var start = kv.Value;

                var newRect = new Rect(
                    start.X + deltaImg.X,
                    start.Y + deltaImg.Y,
                    start.Width,
                    start.Height
                );

                newRect = ClampRectToImage(newRect);
                b.SetFromRect(newRect);
            }

            _overlay.Refresh();
            return;
        }

        // Marquee selection
        if (_isMarqueeSelecting && e.LeftButton == MouseButtonState.Pressed)
        {
            var curImg = _overlay.Viewport.ScreenToImage(pScreen);
            _overlay.SelectionMarqueeImageSpace = NormalizeRect(_marqueeAnchorImg, curImg);
            _overlay.Refresh();
            return;
        }
    }

    private void OnMouseUp(object sender, MouseButtonEventArgs e)
    {
        // Stop pan
        if (e.ChangedButton == MouseButton.Middle && _isPanning)
        {
            _isPanning = false;
            _overlay.ReleaseMouseCapture();
            e.Handled = true;
            return;
        }

        if (e.ChangedButton == MouseButton.Right && _isResizing)
        {
            _isResizing = false;
            _overlay.ReleaseMouseCapture();

            if (_resizeBox is not null)
            {
                var endRect = _resizeBox.ToRect();
                if (endRect != _resizeStartRect)
                {
                    // One undo step. Reuse MoveBoxCommand (it just sets rects).
                    _history.Execute(new MoveBoxCommand(_overlay, _resizeBox, _resizeStartRect, endRect));
                }
            }

            _resizeBox = null;
            _activeHandle = AnnotationGems.Rendering.ResizeHandle.None;

            e.Handled = true;
            return;
        }


        // Finish create box
        if (e.ChangedButton == MouseButton.Right && _isCreatingBox)
        {
            _isCreatingBox = false;
            _overlay.ReleaseMouseCapture();

            var preview = _overlay.PreviewBoxImageSpace;
            _overlay.PreviewBoxImageSpace = null;

            if (preview.HasValue)
            {
                var r = ClampRectToImage(preview.Value);

                if (r.Width >= 1 && r.Height >= 1)
                {
                    var box = new BoundingBox
                    {
                        Id = _nextId++,
                        CategoryId = ActiveCategoryId,
                        CategoryName = ActiveCategoryName,
                        X = r.X,
                        Y = r.Y,
                        Width = r.Width,
                        Height = r.Height
                    };

                    var childRect = r; // already normalized & clamped
                    var parent = FindContainingParent(childRect, ActiveCategoryId);

                    if (parent != null)
                    {
                        box.ParentId = parent.Id;
                    }

                    _history.Execute(new AddBoxCommand(_overlay, box));

                    _lastCreatedBoxSize = new Size(r.Width, r.Height);

                    _overlay.ClearSelection();
                    _overlay.Selected.Add(box);
                }
            }

            _overlay.Refresh();
            e.Handled = true;
            return;
        }

        // Finish group drag
        if (e.ChangedButton == MouseButton.Left && _isDraggingGroup)
        {
            _isDraggingGroup = false;
            _overlay.ReleaseMouseCapture();

            var before = _groupDragStart.ToDictionary(k => k.Key, v => v.Value);
            var after = before.Keys.ToDictionary(b => b, b => b.ToRect());

            bool moved = after.Any(kv =>
            {
                var s = before[kv.Key];
                var a = kv.Value;
                var dx = a.X - s.X;
                var dy = a.Y - s.Y;
                return (dx * dx + dy * dy) >= (0.5 * 0.5);
            });

            if (moved)
                _history.Execute(new MoveBoxesCommand(_overlay, before, after));

            e.Handled = true;
            return;
        }

        // Finish marquee selection
        if (e.ChangedButton == MouseButton.Left && _isMarqueeSelecting)
        {
            _isMarqueeSelecting = false;
            _overlay.ReleaseMouseCapture();

            var mr = _overlay.SelectionMarqueeImageSpace;
            _overlay.SelectionMarqueeImageSpace = null;

            if (mr.HasValue)
            {
                var rect = mr.Value;

                foreach (var ann in _overlay.Annotations)
                {
                    if (ann is BoundingBox b && rect.IntersectsWith(b.ToRect()))
                        _overlay.Selected.Add(b);
                }
            }

            _overlay.Refresh();
            e.Handled = true;
            return;
        }
    }

    private void OnMouseWheel(object sender, MouseWheelEventArgs e)
    {
        var factor = e.Delta > 0 ? 1.15 : (1.0 / 1.15);
        var p = e.GetPosition(_overlay);

        _overlay.Viewport.ZoomAtScreenPoint(p, factor);
        _onViewportChanged?.Invoke();
        _overlay.Refresh();
        e.Handled = true;
    }

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        // Delete selected (group)
        if (e.Key == Key.Delete)
        {
            var selectedBoxes = _overlay.Selected.OfType<BoundingBox>().ToList();
            if (selectedBoxes.Count > 0)
                _history.Execute(new DeleteBoxesCommand(_overlay, selectedBoxes));

            e.Handled = true;
            return;
        }

        // Ctrl shortcuts
        if ((Keyboard.Modifiers & ModifierKeys.Control) != 0)
        {
            // Undo/redo first
            if (e.Key == Key.Z)
            {
                _history.Undo();
                e.Handled = true;
                return;
            }

            if (e.Key == Key.Y)
            {
                _history.Redo();
                e.Handled = true;
                return;
            }

            // Copy selection
            if (e.Key == Key.C)
            {
                _copiedBoxes = _overlay.Selected
                    .OfType<BoundingBox>()
                    .Select(CloneBox)
                    .ToList();

                e.Handled = true;
                return;
            }

            // Paste selection as a group (or fallback to last-created size)
            if (e.Key == Key.V)
            {
                var mouseScreen = Mouse.GetPosition(_overlay);
                var mouseImg = _overlay.Viewport.ScreenToImage(mouseScreen);

                if (_copiedBoxes.Count > 0)
                {
                    var bounds = ComputeBounds(_copiedBoxes);
                    var targetTopLeft = new Point(mouseImg.X + 10, mouseImg.Y + 10);
                    var delta = targetTopLeft - bounds.TopLeft;

                    var pasted = new List<BoundingBox>();
                    foreach (var src in _copiedBoxes)
                    {
                        var b = CloneBox(src);
                        b.Id = _nextId++;
                        b.X += delta.X;
                        b.Y += delta.Y;
                        pasted.Add(b);
                    }

                    _history.Execute(new AddBoxesCommand(_overlay, pasted));

                    _overlay.ClearSelection();
                    foreach (var b in pasted) _overlay.Selected.Add(b);

                    _overlay.Refresh();
                    e.Handled = true;
                    return;
                }

                // Fallback: clone last created size at cursor
                if (_lastCreatedBoxSize.HasValue)
                {
                    var s = _lastCreatedBoxSize.Value;
                    var newBox = new BoundingBox
                    {
                        Id = _nextId++,
                        X = mouseImg.X,
                        Y = mouseImg.Y,
                        Width = s.Width,
                        Height = s.Height
                    };

                    _history.Execute(new AddBoxCommand(_overlay, newBox));

                    _overlay.ClearSelection();
                    _overlay.Selected.Add(newBox);

                    _overlay.Refresh();
                }

                e.Handled = true;
                return;
            }
        }
    }

    private void BeginGroupDrag(Point mouseScreen)
    {
        _isDraggingGroup = true;
        _groupDragAnchorImg = _overlay.Viewport.ScreenToImage(mouseScreen);

        _groupDragStart.Clear();
        foreach (var b in _overlay.Selected.OfType<BoundingBox>())
            _groupDragStart[b] = b.ToRect();

        _overlay.CaptureMouse();
    }

    private static Rect NormalizeRect(Point a, Point b)
    {
        var x1 = Math.Min(a.X, b.X);
        var y1 = Math.Min(a.Y, b.Y);
        var x2 = Math.Max(a.X, b.X);
        var y2 = Math.Max(a.Y, b.Y);
        return new Rect(x1, y1, x2 - x1, y2 - y1);
    }

    private static BoundingBox CloneBox(BoundingBox b)
        => new() { X = b.X, Y = b.Y, Width = b.Width, Height = b.Height };

    private static Rect ComputeBounds(IEnumerable<BoundingBox> boxes)
    {
        bool first = true;
        double x1 = 0, y1 = 0, x2 = 0, y2 = 0;

        foreach (var b in boxes)
        {
            var r = b.ToRect();
            if (first)
            {
                x1 = r.Left; y1 = r.Top; x2 = r.Right; y2 = r.Bottom;
                first = false;
            }
            else
            {
                x1 = Math.Min(x1, r.Left);
                y1 = Math.Min(y1, r.Top);
                x2 = Math.Max(x2, r.Right);
                y2 = Math.Max(y2, r.Bottom);
            }
        }

        return first ? Rect.Empty : new Rect(new Point(x1, y1), new Point(x2, y2));
    }

    private Rect ClampRectToImage(Rect r)
    {
        // TODO: replace with actual current image width/height.
        const double imgW = 1_000_000;
        const double imgH = 1_000_000;

        var x = Math.Max(0, Math.Min(r.X, imgW - 1));
        var y = Math.Max(0, Math.Min(r.Y, imgH - 1));

        var w = Math.Max(1, Math.Min(r.Width, imgW - x));
        var h = Math.Max(1, Math.Min(r.Height, imgH - y));

        return new Rect(x, y, w, h);
    }
}
