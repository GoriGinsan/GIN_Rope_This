# Tested scope — 0.3.0

The published DLL was checked in **Koikatsu CharaStudio / Unity 5.6.2f1**.

- **104,427 standalone assertions** passed for the solver and scene codec.
- **53 runtime checks** passed in an isolated Studio installation.
- The panel and generated geometry were also inspected in framebuffer captures.

## Runtime coverage

- Panel hidden on startup and after a fresh game restart.
- Native Option entry button placement, hit testing, and pointer-event opening.
- Suggested endpoints on an existing 102-bone Rope item.
- Detailed bone list hidden by default, left-aligned names, and manual selection of a middle chain section.
- Automatic vertical and horizontal chains from actual world-space vertex extrema.
- Intermediate counts of 3, 10, and 30, with two additional endpoints.
- Static mesh and single-bone skin conversion, including rotated and nonuniformly scaled items.
- Independent skin-coordinate checks for rest-shape retention and cross-section behavior when bending.
- Original mesh, UV, color, and topology retention; restoration after removal.
- Native visibility changes, hidden-source reconstruction, and protection of separate child-item drivers.
- Safe refusal of unreadable geometry and a zero-length selected axis.
- Native duplication, scene save/load, and restoration after a fresh process restart.
- Restoration of an item saved while hidden, followed by showing and removing its generated rig.

The native entry button used Unity EventSystem raycasts and pointer events in the checks. Selection states and IMGUI layout were checked through a test bridge and framebuffer images. These checks did not use physical OS mouse injection.

## Runtime checklist for changes

1. Confirm the panel starts closed and the entry button remains inside the Option group.
2. Open and close the panel; check endpoint selection and labels.
3. Test an existing authored chain and both automatic axes.
4. Check the original silhouette, materials, vertex attributes, and thickness after moving the handles.
5. Hide, show, remove, and duplicate the item. Check unrelated child items too.
6. Save visible and hidden items, restart Studio, reload, and move the handles again.
7. Confirm an unsupported mesh is refused without extra handles or disabled original renderers.

## Limits

- Authored chains require 3–128 supported nodes. Branches and ambiguous chains may need manual endpoints or may be refused.
- Automatic conversion requires readable mesh data and supports up to 64 source renderers and 500,000 vertices per item.
- Generated skinning does not add topology. Sparse meshes cannot bend as smoothly as densely segmented meshes.
- Automatic conversion freezes the source's current posed shape. Procedural source animation and nonzero morph persistence have not been exhaustively verified.
- Collisions, endpoint twist controls, Koikatsu Sunshine, and every possible third-party plugin combination are outside the tested scope.

## Published DLL

SHA-256 of `GIN Rope This.dll`:

```text
48330e2c2d11b47bed76d9ea0c909af81a6cc0f6c06d8c7de447f5a85714bd29
```
