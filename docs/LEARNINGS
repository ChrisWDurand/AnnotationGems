# Learnings: Issues, Causes, Fixes

This document captures major development issues encountered during early builds, their root causes, and the fixes that worked.

---

## 1) Images initially loaded “cropped” / top-left only

### Symptom
- First image (and sometimes subsequent images) appeared as a small top-left portion of the full image.

### Root Cause
- Viewport fit calculations ran when WPF layout sizing was unstable or when the container layout was interfering with the transform.
- Using a Grid-based layout + render transforms can cause the image to be measured/arranged in ways that don’t match assumed “origin at (0,0)”.

### Fix That Worked
- Use a **Canvas** as the viewer container (`ViewerHost`) and place both `ImageView` and `AnnotationOverlay` inside it at `(0,0)`.
- Explicitly set:
  - `ImageView.Width/Height = PixelWidth/PixelHeight`
  - `Overlay.Width/Height = PixelWidth/PixelHeight`
- Apply viewport transform via `ImageView.RenderTransform` (and overlay draws by converting image->screen via `ZoomPanState`).

Result: images reliably fit the viewer and no longer show a top-left crop.

---

## 2) Box overlay would scale “wrong” or drift during zoom

### Symptom
- Zooming seemed to move boxes away from the objects they were annotating.

### Root Cause
- Image and overlay were not being transformed consistently.
- Sometimes overlay geometry was effectively being scaled twice (layout + transform).

### Fix That Worked
- Single source of truth: `ZoomPanState`.
- Image transform set from `ZoomPanState` (`MatrixTransform`).
- Overlay draws in screen space by converting points through `ZoomPanState.ImageToScreen(...)`.
- Use Canvas layout to avoid implicit scaling/centering.

Result: boxes remain aligned during zoom/pan.

---

## 3) Only some images showed annotations (often only one image)

### Symptom
- Annotations appeared for the “Screenshot” image but not for others.

### Root Cause
- COCO annotation mapping relies on `annotation.image_id == currentImageId`.
- If image IDs are regenerated or modified (e.g., reassigning IDs when scanning a folder), annotations stop matching.
- Additionally: accidental data loss was possible when saving while overlay was empty.

### Fix That Worked
- Treat COCO `images[]` as canonical when it exists:
  - Preserve COCO image IDs.
  - Navigate using COCO image list (filtered to files that exist).
- Prevent accidental clobbering:
  - Do not delete/overwrite existing COCO annotations if the overlay is empty.

Result: correct annotation display across images, stable mapping.

---

## 4) “Fit to annotations” caused repeated “cropped” views

### Symptom
- View looked like it was zoomed into an 846x642 region on large images.

### Root Cause
- Automatic fit was using union of bounding boxes; if annotations only occupied a small region, fit would zoom to that region and feel like a crop.
- In mixed-size datasets, fitting to annotations can feel inconsistent.

### Fix That Worked
- Default to **Fit-to-image** on navigation / load.
- Make “Fit-to-annotations” optional (future UI button/menu).

Result: consistent initial view across image sizes.

---

## 5) Arrow keys not reliably switching images

### Symptom
- Left/right arrows recentered instead of switching images or had inconsistent behavior.

### Root Cause
- Multiple keyboard handlers (PreviewKeyDown + KeyDown) were both trying to navigate/load.
- Image load logic and viewport fit logic were called from multiple places.

### Fix That Worked
- Use a single navigation pipeline:
  - `PreviewKeyDown` -> `NavigateImage(delta)` -> `ShowImageAtIndex(index)`
- Centralize: image load + scheduled fit in `ShowImageAtIndex`.

Result: deterministic navigation behavior.

---
