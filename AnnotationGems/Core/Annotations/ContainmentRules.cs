namespace AnnotationGems.Core.Annotations;

public sealed class ContainmentRules
{
    // Example: ParentCategoryId -> allowed children CategoryIds
    private readonly Dictionary<int, HashSet<int>> _allowedChildren = new();

    public void AllowChild(int parentCategoryId, int childCategoryId)
    {
        if (!_allowedChildren.TryGetValue(parentCategoryId, out var set))
        {
            set = new HashSet<int>();
            _allowedChildren[parentCategoryId] = set;
        }
        set.Add(childCategoryId);
    }

    public bool CanContain(int parentCategoryId, int childCategoryId)
        => _allowedChildren.TryGetValue(parentCategoryId, out var set) && set.Contains(childCategoryId);
}
