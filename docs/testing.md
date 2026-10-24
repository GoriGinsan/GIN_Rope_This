# Tested scope - 1.1.0

## Summary

| Target | Verification |
| --- | --- |
| KK | The previous behavior passed 53 isolated runtime checks. All 183 executable method bodies in the 1.1.0 KK build match that tested behavior, allowing only the startup version label to differ. Assembly references and the embedded button image also match. |
| KKS | 100 runtime checks passed across fresh Studio processes: 85 main/UI/automatic-rig checks and 15 additional resolution, skin-weight, authored-chain, and NodesConstraints checks. |
| Both | The standalone solver and scene-codec suite passed 104,427 assertions with each target's compiler. |

The KKS runtime checks used Unity 2019.4.9f1, BepInEx 5.4.19.0, KKSAPI 1.33, Extended Save 18.2, and NodesConstraints 1.2.2. The KK behavioral baseline used Unity 5.6.2f1. Test helpers and fixture assets are not included in the installation ZIPs.

The native entry button was exercised with Unity EventSystem raycasts and pointer events. Studio object operations, selection, and IMGUI state were exercised through a private test bridge. Framebuffer captures were inspected. These checks did not use physical OS mouse injection.

## Runtime coverage

- Panel hidden on startup and after a fresh restart.
- Native Option entry placement, pointer-event opening/closing, and no workspace button.
- KKS button layout and interaction at 1440x900 and 1280x720.
- Suggested endpoints on an existing 102-bone Rope item.
- Detailed bone list hidden by default, left-aligned names, and an 81-node middle-chain selection.
- Automatic vertical/horizontal chains using actual world-space vertex extrema.
- Intermediate counts of 3, 10, and 30, with two additional endpoints.
- Static and single-bone mesh conversion, including rotated and nonuniformly scaled items.
- Rest-shape and cross-section retention while moving native handles.
- Original mesh, UV, color, and topology retention; restoration after removal.
- Native visibility, hidden-source reconstruction, and separate child-item animation driver retention.
- Refusal of unreadable geometry and a zero-length selected axis without extra handles or disabled source renderers.
- KKS refusal of a five-influence-per-vertex mesh without changing its source or scene objects.
- Native duplication, scene save/load, and generated-rig restoration after a fresh process restart.
- Hidden-scene reload, later visibility, and removal back to the original item.
- KKS authored 102-node chain restoration and NodesConstraints endpoint following before and after a fresh process restart.

## Runtime checklist for changes

1. Confirm the panel starts closed and the entry remains inside the Option group.
2. Check endpoint selection, labels, and both automatic axes.
3. Check silhouette, materials, vertex attributes, and thickness after moving handles.
4. Hide, show, remove, and duplicate items; check unrelated child items too.
5. Save visible and hidden items, restart Studio, reload, and move handles again.
6. Confirm unsupported meshes are refused without leftover handles or renderer changes.
7. Run the same checks separately in KK and KKS when shared behavior changes.

## Limits

- Authored chains require 3-128 supported nodes. Branched or ambiguous chains may need manual endpoints or may be refused.
- Automatic conversion requires readable mesh data, with up to 64 source renderers and 500,000 vertices per item.
- KKS automatic conversion supports up to four skin influences per vertex and safely refuses larger variable-weight sets.
- Generated skinning does not add topology; sparse meshes cannot bend as smoothly as densely segmented meshes.
- Automatic conversion freezes the source's current posed shape. Procedural source animation and nonzero morph persistence have not been exhaustively verified.
- Collisions, endpoint twist controls, every possible third-party plugin combination, and cross-game scene conversion are outside the tested scope.
- KKS was runtime-tested with the dependency versions above; this does not assert testing of every dependency release or mod pack.

## Published DLLs

Both ZIPs install `BepInEx/plugins/GIN Rope This.dll`; install the package matching the game.

| Target | SHA-256 |
| --- | --- |
| KK | `7c4d39dcc3f37ffa369f03e75bf720fbf27a4c49a78fc657eaadb646f2e14495` |
| KKS | `df6fe5744e850ffd0b1893c1f5e5cc500ab8741a6fc82ab87b639a2066235663` |
