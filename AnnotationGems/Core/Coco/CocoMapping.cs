using AnnotationGems.Core.Annotations;

namespace AnnotationGems.Core.Coco;

public static class CocoMapping
{
    // Load: COCO -> BoundingBoxes
    public static List<BoundingBox> ToBoxes(CocoRoot coco, int imageId = 1)
    {
        var boxes = new List<BoundingBox>();

        foreach (var ann in coco.Annotations.Where(a => a.ImageId == imageId))
        {
            if (ann.Bbox is not { Length: 4 }) continue;

            boxes.Add(new BoundingBox
            {
                Id = ann.Id,
                CategoryId = ann.CategoryId,
                X = ann.Bbox[0],
                Y = ann.Bbox[1],
                Width = ann.Bbox[2],
                Height = ann.Bbox[3]
            });
        }

        return boxes;
    }

    // Save: BoundingBoxes -> COCO
    public static CocoRoot FromBoxes(
        IEnumerable<BoundingBox> boxes,
        string imageFileName,
        int imageWidth,
        int imageHeight,
        IEnumerable<(int id, string name)> categories,
        int imageId = 1)
    {
        var coco = new CocoRoot();

        coco.Images.Add(new CocoImage
        {
            Id = imageId,
            FileName = imageFileName,
            Width = imageWidth,
            Height = imageHeight
        });

        coco.Categories.AddRange(categories.Select(c => new CocoCategory { Id = c.id, Name = c.name }));

        foreach (var b in boxes)
        {
            coco.Annotations.Add(new CocoAnnotation
            {
                Id = b.Id,
                ImageId = imageId,
                CategoryId = b.CategoryId,
                Bbox = new[] { b.X, b.Y, b.Width, b.Height }
            });
        }

        return coco;
    }
}
