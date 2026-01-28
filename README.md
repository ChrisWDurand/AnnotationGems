# AnnotationGems

AnnotationGems is a lightweight WPF image annotation tool for creating and editing COCO-style bounding box annotations on very large images. It is designed to work in Visual Studio 2022 Community with no extra VS extensions required.

## Core Concepts

- **Image coordinates are truth**: Bounding boxes are stored in image pixel coordinates.
- **Viewport transforms are view-only**: Zoom/pan affects rendering only, not box coordinates.
- **COCO-like JSON**: Annotations are stored and exported as COCO-style JSON.
- **Project-based workflow**: A project points to an image folder and a working COCO file.

---

## Features

### Project Workflow
- Create/open/save projects.
- Project stores:
  - Project name
  - Image folder path
  - Working COCO annotation JSON (copied/imported or created)

### Image Navigation
- Images are not listed during annotation.
- Use **Left/Right arrow keys** to move through images in the folder.
- Each image loads with its annotations (if present in COCO).

### Viewport / Navigation
- **Mouse wheel**: Zoom in/out centered on cursor.
- **Middle mouse drag** (mouse wheel pressed): Pan image.
- Zoom/pan transforms are applied to both the image and overlay so annotations stay aligned.

### Bounding Box Creation
- **Right-click drag**: Create a new bounding box.

### Selection
- Click to select a box.
- Drag a marquee to select multiple boxes.
- Multi-selection and group operations supported.

### Editing
- Drag a selected box to move it.
- Drag a selected group to move all boxes together.
- Resize a selected box via handles (sides + corners).

### Delete
- Delete selected boxes via **Delete key**.
- Deleting does not reset zoom/pan or refresh the image unexpectedly.

### Export
- Export COCO JSON with current annotations.
- Save project updates the working COCO file.

---

## Architecture Overview

### UI / Rendering
- `MainWindow.xaml`:
  - `ViewerHost` uses a **Canvas** to prevent WPF layout from interfering with transforms.
  - `ImageView` renders the bitmap.
  - `Overlay` (`AnnotationOverlay`) renders bounding boxes.

- `AnnotationOverlay`:
  - Maintains:
    - `Annotations: List<AnnotationBase>`
    - `Selected: HashSet<AnnotationBase>`
    - optional marquee + preview rectangles
  - Handles hit testing and drawing.

### Interaction
- `InteractionController`:
  - Mouse/keyboard state machine:
    - create box
    - select/marquee select
    - drag box
    - drag group
    - resize box
    - pan
  - Operates on overlay annotations directly.
  - (Optional) Undo/redo can wrap operations as commands.

### Viewport
- `ZoomPanState`:
  - `Scale`, `OffsetX`, `OffsetY`
  - `ImageToScreen`, `ScreenToImage`
  - `PanBy`, `ZoomAtScreenPoint`
  - Emits `Changed` event to keep ImageView transform in sync.

### COCO IO
- COCO is loaded/saved as JSON.
- Each image has an ID and filename.
- Each annotation has image_id and bbox [x, y, w, h].

### Project Service
- Creates project folders under:
  - `Documents\AnnotationGems\Projects\`
- Copies/imports COCO into a working file to avoid clobbering the user’s original.

---

## Known Limitations (Current)
- Bounding boxes only (polygons planned).
- Category editing UI not yet present (single-category workflow is stable).
- Per-image viewport persistence is optional/future (current baseline: fit-to-image is stable).

---

## Quick Start
1. Create a new project.
2. Select any image inside the desired image folder.
3. Optionally import an existing COCO JSON.
4. Use right-drag to create boxes; use arrows to navigate images.
5. Save project / export COCO.

---

## License
TBD
