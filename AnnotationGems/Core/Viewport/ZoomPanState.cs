using System.Windows;

namespace AnnotationGems.Core.Viewport;

public sealed class ZoomPanState
{
    public double Scale { get; set; } = 1.0;
    public double OffsetX { get; set; } = 0.0;
    public double OffsetY { get; set; } = 0.0;

    public event Action? Changed;
    private void RaiseChanged() => Changed?.Invoke();

    public Point ImageToScreen(Point p) => new(p.X * Scale + OffsetX, p.Y * Scale + OffsetY);
    public Point ScreenToImage(Point p) => new((p.X - OffsetX) / Scale, (p.Y - OffsetY) / Scale);


    public void PanBy(Vector deltaScreen)
    {
        OffsetX += deltaScreen.X;
        OffsetY += deltaScreen.Y;
        RaiseChanged();
    }

    public void ZoomAtScreenPoint(Point screenPoint, double zoomFactor, double minScale = 0.01, double maxScale = 200.0)
    {
        // Keep the image-point under the cursor stable
        var before = ScreenToImage(screenPoint);

        var newScale = Scale * zoomFactor;
        if (newScale < minScale) newScale = minScale;
        if (newScale > maxScale) newScale = maxScale;

        Scale = newScale;

        var after = ScreenToImage(screenPoint);

        // Move offsets so that before == after in screen space
        // screen = img*scale + offset  => offset changes by (after-before)*scale
        OffsetX += (after.X - before.X) * Scale;
        OffsetY += (after.Y - before.Y) * Scale;

        RaiseChanged();
    }
}
