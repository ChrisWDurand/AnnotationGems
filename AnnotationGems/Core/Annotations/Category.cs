using System.Windows.Media;

namespace AnnotationGems.Core.Annotations;

public sealed class Category
{
    public int Id { get; init; }
    public string Name { get; init; } = "";
    public Brush Stroke { get; init; } = Brushes.Lime;
}
