using AnnotationGems.Interaction;
using System.Windows;
using System.Windows.Media;
using Microsoft.Win32;
using AnnotationGems.Core.Project;
using AnnotationGems.Core.Coco;
using AnnotationGems.Core.Annotations;

using System.IO;
using System.Windows.Input;
using System.Windows.Media.Imaging;

// NOTE: keep WinForms only for FolderBrowserDialog
//using System.Windows.Forms;


namespace AnnotationGems;

public partial class MainWindow : Window
{
    private InteractionController _controller;
    private readonly ProjectService _projects = new();
    private ProjectConfig? _currentProject;

    // TODO: set these from actual current image later
    private string _currentImageFileName = "image.png";
    private int _currentImageWidth = 0;
    private int _currentImageHeight = 0;
    private int _currentImageId = 1;

    private CocoRoot? _coco;
    private List<CocoImage> _images = new();
    private int _imageIndex = 0;

    // ensures we never reuse annotation ids
    private int _nextAnnotationId = 1;


    private List<(int id, string name)> _categories = new()
    {
        (1, "default")
    };

    // Cached pens (frozen)
    private readonly Pen _penClass1;
    private readonly Pen _penClass2;
    private readonly Pen _penDefault;

    // Maintaining distinct window view data
    private readonly Dictionary<int, (double scale, double ox, double oy)> _viewportByImageId = new();


    public MainWindow()
    {
        
        InitializeComponent();

        this.PreviewKeyDown += MainWindow_PreviewKeyDown;


        Overlay.Viewport.Changed += () =>
        {
            SyncImageTransformToViewport();
            Overlay.Refresh();
        };

        _penClass1 = new Pen(Brushes.Lime, 1); _penClass1.Freeze();
        _penClass2 = new Pen(Brushes.Cyan, 1); _penClass2.Freeze();
        _penDefault = new Pen(Brushes.Magenta, 1); _penDefault.Freeze();

        _controller = new InteractionController(Overlay, SyncImageTransformToViewport);

        Overlay.PenProvider = ann => ann.CategoryId switch
        {
            1 => _penClass1,
            2 => _penClass2,
            _ => _penDefault
        };
    }

    private void ShowImageAtIndex(int index)
    {
        if (_currentProject is null || _coco is null) return;
        if (_images.Count == 0) return;

        _imageIndex = Math.Max(0, Math.Min(_images.Count - 1, index));

        LoadCurrentImageAndAnnotations();

        Dispatcher.BeginInvoke(new Action(() =>
        {
            System.Diagnostics.Debug.WriteLine(
                $"[FIT] idx={_imageIndex} file={_currentImageFileName} img={_currentImageWidth}x{_currentImageHeight} host={ViewerHost.ActualWidth}x{ViewerHost.ActualHeight}");

            ResetAndFitViewport();
        }), System.Windows.Threading.DispatcherPriority.Loaded);
    }



    private void SaveViewportForCurrentImage()
    {
        _viewportByImageId[_currentImageId] = (Overlay.Viewport.Scale, Overlay.Viewport.OffsetX, Overlay.Viewport.OffsetY);
    }

    private void RestoreViewportOrFit()
    {
        if (_viewportByImageId.TryGetValue(_currentImageId, out var vp))
        {
            Overlay.Viewport.Scale = vp.scale;
            Overlay.Viewport.OffsetX = vp.ox;
            Overlay.Viewport.OffsetY = vp.oy;
        }
        else
        {
            // First time viewing this image: fit to full image
            Overlay.Viewport.Scale = 1.0;
            Overlay.Viewport.OffsetX = 0.0;
            Overlay.Viewport.OffsetY = 0.0;

            FitViewportToImage();
            SaveViewportForCurrentImage(); // store the baseline fit
        }

        SyncImageTransformToViewport();
        Overlay.Refresh();
    }

    private void RestoreOrFitViewportForCurrentImage()
    {
        // Always run on the next UI tick so ActualWidth/Height and bitmap pixels are valid
        Dispatcher.BeginInvoke(new Action(() =>
        {
            if (_currentImageWidth <= 0 || _currentImageHeight <= 0)
            {
                // If the bitmap didn't load, don't fit
                SyncImageTransformToViewport();
                Overlay.Refresh();
                return;
            }

            if (_viewportByImageId.TryGetValue(_currentImageId, out var vp))
            {
                Overlay.Viewport.Scale = vp.scale;
                Overlay.Viewport.OffsetX = vp.ox;
                Overlay.Viewport.OffsetY = vp.oy;
            }
            else
            {
                // First time: fit and save baseline for this image
                Overlay.Viewport.Scale = 1.0;
                Overlay.Viewport.OffsetX = 0.0;
                Overlay.Viewport.OffsetY = 0.0;

                //FitViewportToImageAndMaybeAnnotations();
                SaveViewportForCurrentImage();
            }

            SyncImageTransformToViewport();
            Overlay.Refresh();

        }), System.Windows.Threading.DispatcherPriority.Loaded);
    }


    private void MainWindow_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (_currentProject is null || _coco is null || _images.Count == 0)
            return;

        if (e.Key == Key.Right)
        {
            NavigateImage(+1);
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Left)
        {
            NavigateImage(-1);
            e.Handled = true;
            return;
        }
    }

    private void ResetAndFitViewport()
    {

        // Debugging the image fit results
        System.Diagnostics.Debug.WriteLine($"ViewerHost size: {ViewerHost.ActualWidth} x {ViewerHost.ActualHeight}");

        // If layout isn't ready yet, delay the fit.
        // This is the root cause of the "cropped top-left" first image.
        if (ViewerHost.ActualWidth < 10 || ViewerHost.ActualHeight < 10)
        {
            Dispatcher.BeginInvoke(new Action(ResetAndFitViewport),
                System.Windows.Threading.DispatcherPriority.Loaded);
            return;
        }

        Overlay.Viewport.Scale = 1.0;
        Overlay.Viewport.OffsetX = 0.0;
        Overlay.Viewport.OffsetY = 0.0;

        FitViewportToImage(); // Fit FULL IMAGE (not boxes)
        System.Diagnostics.Debug.WriteLine($"Viewport: scale={Overlay.Viewport.Scale:F4} off=({Overlay.Viewport.OffsetX:F1},{Overlay.Viewport.OffsetY:F1})");


        SyncImageTransformToViewport();
        Overlay.Refresh();
    }




    private void NavigateImage(int delta)
    {
        if (_currentProject is null || _coco is null || _images.Count == 0)
            return;

        // Save current boxes before switching
        CommitOverlayAnnotationsToCoco();
        _projects.SaveWorkingCoco(_currentProject, _coco);

        var newIndex = Math.Max(0, Math.Min(_images.Count - 1, _imageIndex + delta));
        if (newIndex == _imageIndex) return;

        ShowImageAtIndex(newIndex);
    }



    private void EnsureImagesFromFolder(ProjectConfig project)
    {
        if (_coco is null) return;

        // Files on disk (just names)
        var filesOnDisk = Directory.EnumerateFiles(project.ImageFolder)
            .Where(IsImageFile)
            .Select(Path.GetFileName)
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .Select(n => n!)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (_coco.Images.Count > 0)
        {
            // COCO already defines the image IDs. Do NOT add or change IDs.
            // Just use COCO list for navigation, but optionally filter out missing disk files.
            _images = _coco.Images
                .Where(i => filesOnDisk.Contains(i.FileName))
                .OrderBy(i => i.FileName, StringComparer.OrdinalIgnoreCase)
                .ToList();

            return;
        }

        // If COCO has no images (new project), build images[] from folder scan
        var files = filesOnDisk
            .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
            .ToList();

        int id = 0;
        foreach (var fn in files)
        {
            _coco.Images.Add(new CocoImage { Id = id++, FileName = fn });
        }

        _images = _coco.Images
            .OrderBy(i => i.FileName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }



    private void NewProject_Click(object sender, RoutedEventArgs e)
    {
        var name = TextPromptWindow.Show(this, "Project name:", "New Project");
        if (string.IsNullOrWhiteSpace(name)) return;

        var pickAnyFile = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Select any image inside the image folder",
            Filter = "Images (*.png;*.jpg;*.jpeg;*.tif;*.tiff;*.bmp)|*.png;*.jpg;*.jpeg;*.tif;*.tiff;*.bmp|All files (*.*)|*.*",
            CheckFileExists = true
        };

        if (pickAnyFile.ShowDialog(this) != true) return;

        var imageFolder = System.IO.Path.GetDirectoryName(pickAnyFile.FileName);
        if (string.IsNullOrWhiteSpace(imageFolder)) return;

        // Pick COCO JSON to import
        string? cocoPath = null;

        var import = MessageBox.Show(
            this,
            "Do you want to import an existing COCO annotation file?",
            "Import Annotations",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (import == MessageBoxResult.Yes)
        {
            var ofd = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Select COCO JSON to import",
                Filter = "COCO JSON (*.json)|*.json|All files (*.*)|*.*",
                CheckFileExists = true
            };

            if (ofd.ShowDialog(this) != true) return;
            cocoPath = ofd.FileName;
        }

        _currentProject = _projects.CreateProject(name, imageFolder, cocoPath);
        LoadProjectIntoEditor(_currentProject);

    }


    private void OpenProject_Click(object sender, RoutedEventArgs e)
    {
        var ofd = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Open AnnotationGems Project",
            Filter = "AnnotationGems Project (project.agproj.json)|project.agproj.json|JSON (*.json)|*.json|All files (*.*)|*.*",
            InitialDirectory = ProjectService.ProjectsRoot
        };

        if (ofd.ShowDialog(this) != true) return;

        _currentProject = _projects.LoadProject(ofd.FileName);
        LoadProjectIntoEditor(_currentProject);
    }

    private void SaveProject_Click(object sender, RoutedEventArgs e)
    {
        if (_currentProject is null || _coco is null) return;

        // Commit current overlay edits into COCO for current image
        CommitOverlayAnnotationsToCoco();

        // Save working coco + config
        _projects.SaveWorkingCoco(_currentProject, _coco);
        _projects.SaveProjectConfig(_currentProject);
    }


    private void ExportAnnotations_Click(object sender, RoutedEventArgs e)
    {
        if (_currentProject is null || _coco is null) return;

        CommitOverlayAnnotationsToCoco();

        var sfd = new Microsoft.Win32.SaveFileDialog
        {
            Title = "Export Annotations",
            Filter = "COCO JSON (*.json)|*.json",
            FileName = "annotations.export.json",
            OverwritePrompt = true
        };

        if (sfd.ShowDialog(this) != true) return;

        _projects.ExportCoco(sfd.FileName, _coco);
    }


    private void LoadProjectIntoEditor(ProjectConfig project)
    {
        // 0) Clear any per-image viewport cache (not used for now, but harmless)
        _viewportByImageId.Clear();

        // 1) Load working COCO into memory
        _coco = _projects.LoadWorkingCoco(project);
        System.Diagnostics.Debug.WriteLine($"COCO annotations loaded: {_coco.Annotations.Count}");
        System.Diagnostics.Debug.WriteLine($"COCO annotations by image: " +
            string.Join(", ", _coco.Images.Select(i => $"{i.Id}:{_coco.Annotations.Count(a => a.ImageId == i.Id)}")));


        // 2) Ensure at least one category
        if (_coco.Categories.Count == 0)
            _coco.Categories.Add(new CocoCategory { Id = 1, Name = "default" });

        _categories = _coco.Categories.Select(c => (c.Id, c.Name)).ToList();

        // 3) Build/merge images from folder into COCO and populate _images (sorted)
        EnsureImagesFromFolder(project);
        System.Diagnostics.Debug.WriteLine($"Images found: {_images.Count}");

        // Persist updated images[] list into the working file
        _projects.SaveWorkingCoco(project, _coco);

        if (_images.Count == 0)
        {
            ImageView.Source = null;
            Overlay.Annotations.Clear();
            Overlay.ClearSelection();
            Overlay.Refresh();
            return;
        }

        _nextAnnotationId = _coco.Annotations.Count > 0 ? _coco.Annotations.Max(a => a.Id) + 1 : 1;

        // Show first image (and fit it reliably)
        ShowImageAtIndex(0);
    }



    private static bool IsImageFile(string path)
    {
        var ext = Path.GetExtension(path).ToLowerInvariant();
        return ext is ".png" or ".jpg" or ".jpeg" or ".bmp" or ".tif" or ".tiff";
    }

    private void LoadCurrentImageAndAnnotations()
    {
        if (_currentProject is null || _coco is null) return;
        if (_images.Count == 0) return;

        var img = _images[_imageIndex];

        // Load the bitmap into the Image control
        var fullPath = Path.Combine(_currentProject.ImageFolder, img.FileName);
        if (File.Exists(fullPath))
        {
            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.CacheOption = BitmapCacheOption.OnLoad; // lets file be released
            bmp.UriSource = new Uri(fullPath, UriKind.Absolute);
            bmp.EndInit();
            bmp.Freeze();
            ImageView.Source = bmp;

            // Make the overlay exactly the same logical size as the image (in pixels)
            Overlay.Width = bmp.PixelWidth;
            Overlay.Height = bmp.PixelHeight;

            // Optional: also set ImageView size explicitly to avoid layout scaling
            ImageView.Width = bmp.PixelWidth;
            ImageView.Height = bmp.PixelHeight;

            _currentImageWidth = bmp.PixelWidth;
            _currentImageHeight = bmp.PixelHeight;
            
            // Debugging to confirm sizes
            //System.Diagnostics.Debug.WriteLine($"Current image: {_currentImageFileName} size={_currentImageWidth}x{_currentImageHeight} index={_imageIndex}");
            System.Diagnostics.Debug.WriteLine($"Current image: {img.FileName} size={_currentImageWidth}x{_currentImageHeight} index={_imageIndex}");

            _currentImageFileName = img.FileName;
            _currentImageId = img.Id;
        }
        else
        {
            ImageView.Source = null;
            _currentImageWidth = 0;
            _currentImageHeight = 0;
            _currentImageFileName = img.FileName;
            _currentImageId = img.Id;
        }
        var count = _coco.Annotations.Count(a => a.ImageId == _currentImageId);
        System.Diagnostics.Debug.WriteLine($"Annotations for image_id={_currentImageId}: {count}");

        // Debugging image display in which the views are black/background when opening a project
        System.Diagnostics.Debug.WriteLine($"Loaded image: {fullPath}  size={_currentImageWidth}x{_currentImageHeight}");


        // Load annotations for this image id into overlay
        Overlay.Annotations.Clear();
        Overlay.ClearSelection();

        foreach (var ann in _coco.Annotations.Where(a => a.ImageId == _currentImageId))
        {
            if (ann.Bbox is not { Length: 4 }) continue;

            Overlay.Annotations.Add(new BoundingBox
            {
                Id = ann.Id,
                CategoryId = ann.CategoryId,
                X = ann.Bbox[0],
                Y = ann.Bbox[1],
                Width = ann.Bbox[2],
                Height = ann.Bbox[3]
            });
        }

        Overlay.Refresh();

        // Also update the window title so you can see which image you're on
        Title = $"{_currentProject.ProjectName}  —  {img.FileName}  ({_imageIndex + 1}/{_images.Count})";
    }

    private void FitViewportToImage()
    {
        if (_currentImageWidth <= 0 || _currentImageHeight <= 0) return;

        var viewW = ViewerHost.ActualWidth;
        var viewH = ViewerHost.ActualHeight;
        if (viewW <= 1 || viewH <= 1) return;

        const double margin = 20;
        var availW = Math.Max(1, viewW - margin * 2);
        var availH = Math.Max(1, viewH - margin * 2);

        var scale = Math.Min(availW / _currentImageWidth, availH / _currentImageHeight);

        // center image
        var viewCenterX = viewW / 2.0;
        var viewCenterY = viewH / 2.0;

        var imgCenterX = _currentImageWidth / 2.0;
        var imgCenterY = _currentImageHeight / 2.0;

        Overlay.Viewport.Scale = scale;
        Overlay.Viewport.OffsetX = viewCenterX - imgCenterX * scale;
        Overlay.Viewport.OffsetY = viewCenterY - imgCenterY * scale;
    }

    private void CommitOverlayAnnotationsToCoco()
    {
        if (_coco is null) return;

        // If overlay has no boxes, DO NOT clobber existing annotations.
        // This prevents accidental data loss when something fails to load.
        var boxes = Overlay.Annotations.OfType<BoundingBox>().ToList();
        if (boxes.Count == 0)
        {
            System.Diagnostics.Debug.WriteLine($"[WARN] Not committing: overlay empty for image_id={_currentImageId} (avoids clobber).");
            return;
        }

        // Remove existing annotations for this image
        _coco.Annotations.RemoveAll(a => a.ImageId == _currentImageId);

        // Add current overlay boxes
        foreach (var box in boxes)
        {
            if (box.Id <= 0)
                box.Id = _nextAnnotationId++;

            _coco.Annotations.Add(new CocoAnnotation
            {
                Id = box.Id,
                ImageId = _currentImageId,
                CategoryId = box.CategoryId,
                Bbox = new[] { box.X, box.Y, box.Width, box.Height }
            });
        }
    }



    private void SyncImageTransformToViewport()
    {
        // Viewport.ImageToScreen: screen = image * scale + offset
        var vp = Overlay.Viewport;

        var m = new System.Windows.Media.Matrix(
            vp.Scale, 0,
            0, vp.Scale,
            vp.OffsetX, vp.OffsetY);

        ImageView.RenderTransform = new System.Windows.Media.MatrixTransform(m);
    }



}
