using System.Windows;

namespace AnnotationGems.Core.Annotations;

public sealed class BoundingBox : AnnotationBase
{
    public double X { get; set; }
    public double Y { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }
    public int? ParentId { get; set; }  // null if no parent
    public Rect ToRect() => new(X, Y, Width, Height);

    public void SetFromRect(Rect r)
    {
        X = r.X; Y = r.Y; Width = r.Width; Height = r.Height;
    }
}
