namespace AnnotationGems.Core.Annotations;

public abstract class AnnotationBase
{
    public int Id { get; set; }

    // Category/Class identity
    public int CategoryId { get; set; } = 1;     // default class
    public string CategoryName { get; set; } = "default";
}

