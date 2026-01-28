namespace AnnotationGems.Core.Project;

public sealed class ProjectConfig
{
    public string ProjectName { get; set; } = "";
    public string ProjectFolder { get; set; } = "";

    public string ImageFolder { get; set; } = "";

    // Tool-owned file we edit
    public string WorkingAnnotationsPath { get; set; } = "";

    // Backup of the user-supplied original (never written by tool)
    public string? OriginalAnnotationsPath { get; set; }

    // The .agproj.json file path for convenience
    public string ProjectFilePath { get; set; } = "";
}