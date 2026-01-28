using System.Text.Json.Serialization;

namespace AnnotationGems.Core.Coco;

public sealed class CocoRoot
{
    [JsonPropertyName("images")]
    public List<CocoImage> Images { get; set; } = new();

    [JsonPropertyName("annotations")]
    public List<CocoAnnotation> Annotations { get; set; } = new();

    [JsonPropertyName("categories")]
    public List<CocoCategory> Categories { get; set; } = new();
}

public sealed class CocoImage
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("file_name")]
    public string FileName { get; set; } = "";

    [JsonPropertyName("width")]
    public int? Width { get; set; }

    [JsonPropertyName("height")]
    public int? Height { get; set; }
}

public sealed class CocoCategory
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";
}

public sealed class CocoAnnotation
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("image_id")]
    public int ImageId { get; set; }

    [JsonPropertyName("category_id")]
    public int CategoryId { get; set; }

    // COCO bbox: [x, y, width, height]
    [JsonPropertyName("bbox")]
    public double[] Bbox { get; set; } = Array.Empty<double>();

    // Optional (ignore for now)
    [JsonPropertyName("segmentation")]
    public object? Segmentation { get; set; }

    [JsonPropertyName("iscrowd")]
    public int? IsCrowd { get; set; }
}
