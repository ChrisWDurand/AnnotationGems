using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using AnnotationGems.Core.Annotations;
using AnnotationGems.Core.Viewport;

namespace AnnotationGems.Rendering;

public enum ResizeHandle
{
    None,
    N, NE, E, SE, S, SW, W, NW
}

public sealed class AnnotationOverlay : FrameworkElement
{
    public ZoomPanState Viewport { get; set; } = new();

    // We'll keep this as a List for now (fast + simple).
    public List<AnnotationBase> Annotations { get; } = new();

    public HashSet<AnnotationBase> Selected { get; } = new();

    public Rect? SelectionMarqueeImageSpace { get; set; }

    // Optional: show preview while creating a box
    public Rect? PreviewBoxImageSpace { get; set; }

    public Func<AnnotationBase, Pen>? PenProvider { get; set; }

    public void Refresh() => InvalidateVisual();

    // Handle rendering / hit-testing in SCREEN pixels (not image pixels)
    private const double HandleDrawSizePx = 8;   // visible square size
    private const double HandleHitPadPx = 6;     // extra hit padding around square

    protected override void OnRender(DrawingContext dc)
    {
        var defaultPen = new Pen(Brushes.Lime, 1);
        defaultPen.Freeze();

        var selectedPen = new Pen(Brushes.Yellow, 2);
        selectedPen.Freeze();

        // 1) Draw all boxes
        foreach (var ann in Annotations)
        {
            if (ann is BoundingBox box)
            {
                var isSelected = Selected.Contains(ann);
                var pen = isSelected ? selectedPen : (PenProvider?.Invoke(ann) ?? defaultPen);
                DrawBox(dc, box, pen);
            }
        }

        // 2) Marquee preview
        if (SelectionMarqueeImageSpace is Rect mr)
        {
            var marqueePen = new Pen(Brushes.Orange, 1);
            marqueePen.Freeze();
            DrawRect(dc, mr, marqueePen);
        }

        // 3) Create-box preview
        if (PreviewBoxImageSpace is Rect pr)
        {
            var previewPen = new Pen(Brushes.Orange, 1);
            previewPen.Freeze();
            DrawRect(dc, pr, previewPen);
        }

        // 4) Resize handles (only when exactly one box is selected)
        var selectedBox = GetSingleSelectedBoxOrNull();
        if (selectedBox is not null)
        {
            DrawHandles(dc, selectedBox.ToRect());
        }
    }

    private BoundingBox? GetSingleSelectedBoxOrNull()
    {
        // Avoid SingleOrDefault() because it throws if multiple selected.
        if (Selected.Count != 1) return null;
        return Selected.OfType<BoundingBox>().FirstOrDefault();
    }

    private void DrawBox(DrawingContext dc, BoundingBox box, Pen pen)
    {
        DrawRect(dc, box.ToRect(), pen);
    }

    private void DrawRect(DrawingContext dc, Rect imageRect, Pen pen)
    {
        var tl = Viewport.ImageToScreen(imageRect.TopLeft);
        var br = Viewport.ImageToScreen(imageRect.BottomRight);
        dc.DrawRectangle(null, pen, new Rect(tl, br));
    }

    // ---------- Handle points (IMAGE space) ----------

    public static IEnumerable<(ResizeHandle handle, Point imgPoint)> GetHandlePoints(Rect r)
    {
        var cx = r.X + r.Width / 2;
        var cy = r.Y + r.Height / 2;

        yield return (ResizeHandle.NW, new Point(r.Left, r.Top));
        yield return (ResizeHandle.N, new Point(cx, r.Top));
        yield return (ResizeHandle.NE, new Point(r.Right, r.Top));

        yield return (ResizeHandle.W, new Point(r.Left, cy));
        yield return (ResizeHandle.E, new Point(r.Right, cy));

        yield return (ResizeHandle.SW, new Point(r.Left, r.Bottom));
        yield return (ResizeHandle.S, new Point(cx, r.Bottom));
        yield return (ResizeHandle.SE, new Point(r.Right, r.Bottom));
    }

    // ---------- Handle drawing / hit-testing (SCREEN space) ----------

    private Rect HandleRectScreen(Point imgPt)
    {
        var s = Viewport.ImageToScreen(imgPt);

        // Hit rect is larger than draw rect to make it easy to grab
        var hitSize = HandleDrawSizePx + HandleHitPadPx * 2;
        return new Rect(s.X - hitSize / 2, s.Y - hitSize / 2, hitSize, hitSize);
    }

    private Rect HandleDrawRectScreen(Point imgPt)
    {
        var s = Viewport.ImageToScreen(imgPt);
        return new Rect(s.X - HandleDrawSizePx / 2, s.Y - HandleDrawSizePx / 2, HandleDrawSizePx, HandleDrawSizePx);
    }

    private void DrawHandles(DrawingContext dc, Rect imageRect)
    {
        foreach (var (_, imgPt) in GetHandlePoints(imageRect))
        {
            var drawRect = HandleDrawRectScreen(imgPt);
            dc.DrawRectangle(Brushes.White, null, drawRect);
        }
    }

    public ResizeHandle HitTestHandle(Point screenPoint, BoundingBox box)
    {
        var r = box.ToRect();

        // Prefer sides over corners so clicking near side midpoint never "becomes a corner".
        // Also stable ordering avoids random selection when overlapping.
        ResizeHandle[] order =
        {
            ResizeHandle.N, ResizeHandle.E, ResizeHandle.S, ResizeHandle.W,
            ResizeHandle.NW, ResizeHandle.NE, ResizeHandle.SE, ResizeHandle.SW
        };

        var map = GetHandlePoints(r).ToDictionary(x => x.handle, x => x.imgPoint);

        foreach (var h in order)
        {
            var hitRect = HandleRectScreen(map[h]);
            if (hitRect.Contains(screenPoint))
                return h;
        }

        return ResizeHandle.None;
    }

    // ---------- Box hit-testing ----------

    public BoundingBox? HitTestBox(Point screenPoint)
    {
        var pImg = Viewport.ScreenToImage(screenPoint);

        // Iterate from top-most to bottom-most (last drawn = last in list)
        for (int i = Annotations.Count - 1; i >= 0; i--)
        {
            if (Annotations[i] is BoundingBox b && b.ToRect().Contains(pImg))
                return b;
        }
        return null;
    }

    protected override HitTestResult HitTestCore(PointHitTestParameters hitTestParameters)
    {
        // Always treat the overlay as hit-testable so it receives mouse input
        return new PointHitTestResult(this, hitTestParameters.HitPoint);
    }

    public void ClearSelection() => Selected.Clear();
}
