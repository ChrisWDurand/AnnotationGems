using System.IO;
using System.Text.Json;

namespace AnnotationGems.Core.Coco;

public static class CocoIO
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = null,
        WriteIndented = true
    };

    public static CocoRoot Load(string path)
    {
        var json = File.ReadAllText(path);
        var root = JsonSerializer.Deserialize<CocoRoot>(json, Options);
        return root ?? new CocoRoot();
    }

    public static void Save(string path, CocoRoot root)
    {
        var json = JsonSerializer.Serialize(root, Options);
        File.WriteAllText(path, json);
    }
}
