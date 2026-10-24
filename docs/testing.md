# Tested scope - 1.1.2

## Summary

| Target | Current-build verification |
| --- | --- |
| KK | 180 native runtime checks; 104,503 core assertions and 72,303 subdivision assertions. |
| KKS | 181 native runtime checks; 104,503 core assertions and 105,232 subdivision assertions. |

KK used Unity 5.6.2f1. KKS used Unity 2019.4.9f1, BepInEx 5.4.19.0, KKSAPI 1.33, Extended Save 18.2, and NodesConstraints 1.2.2. The Option entry was exercised with Unity EventSystem raycasts and pointer events. Item operations were driven through native Studio APIs; framebuffer captures were visually inspected. Standalone Mesh substitutes verify CPU geometry planning and interpolation, not native rendering.

## Cylinder deformation

Both native Cylinder variants (Normal and Character) were stretched along X, Y, Z, and an oblique direction before conversion, then bent with endpoint separation reduced to 55% of authored length. Long private-copy triangles were subdivided, with a maximum axial span of one quarter of a physics interval.

Actual triangle cross-sections were measured at 25%, 50%, and 75% of the chain. Both lower and upper area/width bounds were checked, together with section centering and missing sections. Rest-coordinate localization excludes a distant arm of a U bend from the same measurement plane. All 48 final cross-sections retained approximately 99.8-100.1% of their original area in this test setup. This is measured coverage, not a guarantee for every shape or pose.

The original 196-vertex assets stayed unchanged. Private runtime copies used roughly 2,000-2,300 vertices. Their original surface, material boundaries, UVs, and colors were retained; new vertex data was interpolated. Rigid rendering frames preserve the section while the requested physics-node count remains unchanged.

## Additional runtime coverage

- Closed startup panel, native Option entry, pointer-event toggling, and no Workspace button.
- Automatic principal-axis selection at X90, Z90, arbitrary rotations, nonuniform scales, internally rotated meshes, and stable symmetric geometry.
- Intermediate counts 3, 10, and 30; static, single-bone, and flat elongated sources.
- Rest-shape retention, finite handle motion, source preservation, and removal back to the original renderer.
- Independent duplication and exact local endpoint retention after item rotation.
- Visible/hidden scene save/load across a fresh process restart.
- Actual 1.1.1 scenes and legacy axis metadata restored without refitting saved endpoints.
- Collapsed and unreadable meshes rejected without extra handles or source changes; KKS five-influence meshes safely rejected.

Subdivision CPU tests additionally cover shared-edge conformity, UV seams, multiple submeshes, flat cylinder caps, posed vertex data, vertex/channel validation, work budgets, and index limits. KK refuses oversized generated meshes safely; KKS uses 32-bit indices when needed.

## Limits

- Automatic conversion needs readable triangle meshes when subdivision is required. The item limit is 500,000 vertices; KK generated meshes are limited to 65,535 vertices each.
- Rendering frames are bounded to 128. Complex meshes with many distinct axial stations use a finite sample grid; extremely thin or irregular geometry is not exhaustively covered.
- Principal direction depends on the visible vertex distribution. Symmetric shapes use a fixed item-relative preference.
- Conversion freezes the source's posed shape. Procedural source animation, morph persistence, every plugin combination, and cross-game scene conversion are not exhaustively tested.
- Collisions and user-controlled endpoint twist are not implemented. Self-intersection is possible when the simulated chain folds onto itself.
- Authored-chain and NodesConstraints checks from release 1.1.0 remain historical evidence; they were not all repeated for 1.1.2.

## Release DLLs

Each ZIP contains only BepInEx/plugins/GIN Rope This.dll.

| Target | SHA-256 |
| --- | --- |
| KK | `2fdf77425996db47861a78ee65e338352c295cb7b2dedbabed3372d923e8ef9a` |
| KKS | `c5a84e2a362ab9d08c0a7f7dc105f0726a56370ca2986a8aa7abfe8458887b77` |
