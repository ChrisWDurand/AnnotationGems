# Planned Changes

This document lists likely next steps after the “stable baseline” milestone.

---

## 1) Categories & Visualization
- Support multiple categories from COCO `categories[]`.
- UI to select active category (dropdown or hotkeys 1..9).
- Per-category colors (already partially supported via `PenProvider`).

## 2) Polygons
- Add polygon annotation type:
  - click-to-add vertices
  - close polygon
  - drag vertex
  - move polygon
- Export COCO segmentation format.

## 3) Undo / Redo
- Command-based undo/redo stack:
  - add boxes
  - delete boxes
  - move boxes (single/group)
  - resize boxes
- Coalesce micro-moves into a single action.

## 4) Per-image View Persistence (Optional)
- Remember zoom/pan per image so returning restores context.
- Keep fit-to-image as initial baseline for first visit.

## 5) Annotation List / Inspector Panel
- Panel listing boxes for current image:
  - click to select
  - show category + id
  - quick delete
  - jump-to box

## 6) Stability / Safety
- Harden COCO mapping:
  - never mutate COCO IDs unexpectedly
  - warn if image filenames missing
  - avoid clobber if overlay empty
- Add explicit “Save” indicator and/or autosave toggle.

## 7) Performance (Large Images)
- Consider tiled image rendering for 100MP–1000MP images:
  - decode only visible region
  - downsample based on zoom level
- Avoid holding full-resolution bitmaps in memory when not needed.

---
