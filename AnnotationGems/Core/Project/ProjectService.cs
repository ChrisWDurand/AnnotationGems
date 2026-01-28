using AnnotationGems.Core.Coco;
using System.IO;
using System.Text.Json;

namespace AnnotationGems.Core.Project;

public sealed class ProjectService
{
    public static string ProjectsRoot =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                     "AnnotationGems", "Projects");

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = null,
        WriteIndented = true
    };

    public ProjectConfig CreateProject(string projectName, string imageFolder, string? cocoImportPath)
    {
        Directory.CreateDirectory(ProjectsRoot);

        var safeName = MakeSafeFolderName(projectName);
        var projectFolder = Path.Combine(ProjectsRoot, safeName);
        Directory.CreateDirectory(projectFolder);

        var projectFilePath = Path.Combine(projectFolder, "project.agproj.json");

        var workingPath = Path.Combine(projectFolder, "annotations.working.json");
        var originalCopyPath = Path.Combine(projectFolder, "annotations.original.json");

        if (!string.IsNullOrWhiteSpace(cocoImportPath))
        {
            File.Copy(cocoImportPath, originalCopyPath, overwrite: true);
            File.Copy(cocoImportPath, workingPath, overwrite: true);
        }
        else
        {
            // Create empty COCO
            var empty = new CocoRoot
            {
                Categories = new List<CocoCategory>
        {
            new() { Id = 1, Name = "default" }
        }
            };

            CocoIO.Save(workingPath, empty);
        }


        var cfg = new ProjectConfig
        {
            ProjectName = projectName,
            ProjectFolder = projectFolder,
            ProjectFilePath = projectFilePath,
            ImageFolder = imageFolder,
            WorkingAnnotationsPath = workingPath,
            OriginalAnnotationsPath = originalCopyPath
        };

        SaveProjectConfig(cfg);
        return cfg;
    }

    public ProjectConfig LoadProject(string projectFilePath)
    {
        var json = File.ReadAllText(projectFilePath);
        var cfg = JsonSerializer.Deserialize<ProjectConfig>(json, Options)
                  ?? throw new InvalidOperationException("Invalid project file.");

        // Ensure these are set even if older project files lacked them
        cfg.ProjectFilePath = projectFilePath;

        if (string.IsNullOrWhiteSpace(cfg.ProjectFolder))
            cfg.ProjectFolder = Path.GetDirectoryName(projectFilePath) ?? "";

        return cfg;
    }

    public void SaveProjectConfig(ProjectConfig cfg)
    {
        Directory.CreateDirectory(cfg.ProjectFolder);
        var json = JsonSerializer.Serialize(cfg, Options);
        File.WriteAllText(cfg.ProjectFilePath, json);
    }

    public CocoRoot LoadWorkingCoco(ProjectConfig cfg)
    {
        return CocoIO.Load(cfg.WorkingAnnotationsPath);
    }

    public void SaveWorkingCoco(ProjectConfig cfg, CocoRoot coco)
    {
        CocoIO.Save(cfg.WorkingAnnotationsPath, coco);
    }

    public void ExportCoco(string exportPath, CocoRoot coco)
    {
        CocoIO.Save(exportPath, coco);
    }

    private static string MakeSafeFolderName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var cleaned = new string(name.Select(ch => invalid.Contains(ch) ? '_' : ch).ToArray()).Trim();
        return string.IsNullOrWhiteSpace(cleaned) ? "Project" : cleaned;
    }
}
