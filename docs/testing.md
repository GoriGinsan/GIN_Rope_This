# Tested scope - 1.1.3

## Summary

| Target | Native runtime checks | Core assertions | Motion assertions | Subdivision assertions |
| --- | --- | --- | --- | --- |
| KK | 177 | 104,503 | 1,479 | 72,303 |
| KKS | 177 | 104,503 | 1,479 | 105,232 |

The same DLLs used for native verification are included in the installation ZIPs. Item operations used native Studio APIs; actual camera captures were inspected. Standalone Mesh substitutes test CPU geometry and interpolation rather than native rendering.

## Frame timing and Video Export

- Native motion was compared at 10, 24, 30, 60, 120, 144, and 240 FPS, plus alternating 3/47 ms frame intervals. A common linear handle path produced 240 physics steps over two seconds at every rate, with position differences below 0.000001 world units.
- Twenty actual camera renders changed neither solver positions, Verlet velocity history, nor the physics clock. Standalone tests also compare the next physics step after repeated renders on a moving rope.
- Actual Video Export **2.1.1** produced **223 PNG frames per game** at 24/60 FPS, with processing delays up to 50 ms and time scales 0, 0.5, 1, and 2. Capture intervals were verified after the initial recording frame, which can precede Unity's next clock update.
- An existing **102-bone Rope** followed moving Studio objects through actual NodesConstraints links. Every captured endpoint stayed within 0.0001 world units of its target. A Timeline-synchronized rope was also captured at **1 FPS**.
- Zero time scale, pause, large handle moves while paused, scene save, fresh process restart, and saved NodesConstraints links were checked.

Standalone motion tests additionally cover fractional rates including 59.94 FPS, piecewise-linear handle paths, interpolation, camera purity, invalid inputs, pause/resume, and the bounded two-second step budget.

### Tested export setup

| Component | KK | KKS |
| --- | --- | --- |
| Unity | 5.6.2f1 | 2019.4.9f1 |
| BepInEx | 5 | 5.4.19.0 |
| Modding API | KKAPI 1.48.1 | KKSAPI 1.43 |
| Extended Save | 21.1.4.1 | 21.1.2 |
| Screenshot Manager | 21.1.2 | 21.1.2 |
| NodesConstraints | 1.6.3 | 1.2.2 |
| Video Export | 2.1.1 | 2.1.1 |
| Timeline | 1.5.6 | 1.1.5 |

These are tested combinations, not claims about the newest dependencies. Video Export and its screenshot dependencies are optional for Rope This itself.

## Cylinder regression

Both native Cylinder variants were stretched along X, Y, Z, and an oblique direction, converted, then bent with endpoint separation reduced to 55% of authored length. All 48 measured cross-sections across KK and KKS retained approximately 99.8-100.1% of their original area. Original meshes remained unchanged, and removal restored the original renderer.

Subdivision and skinning are unchanged from [1.1.2](https://github.com/GoriGinsan/GIN_Rope_This/blob/v1.1.2/docs/testing.md). Earlier axis, duplication, hidden-scene, unsupported-input, and legacy-codec checks remain historical evidence; they were not all repeated in the native 1.1.3 suites. Scene schema remains 3.

## Limits

- Verification uses Video Export's built-in Normal PNG capture. Final video encoding, every capture backend, and every plugin combination are not exhaustively tested.
- Moving targets are sampled once per scene frame, then interpolated at physics ticks. Nonlinear animation at very low FPS can differ from a finer sample. Motion already present in a target animation remains visible.
- Ordinary live gaps over half a second and capture gaps over two seconds reset physics to avoid an unbounded backlog. Rendering has a one-physics-step interpolation delay; visible endpoints follow current handles.
- Automatic conversion needs supported readable geometry. The item limit is 500,000 vertices; KK generated meshes are limited to 65,535 vertices each. Rendering frames are bounded to 128. KKS automatic conversion safely refuses more than four skin influences per vertex.
- Collisions and user-controlled endpoint twist are not implemented. Self-intersection is possible. Procedural source animation, morph persistence, and cross-game scene conversion are not exhaustively tested.

## Release DLLs

Each installation ZIP contains only `BepInEx/plugins/GIN Rope This.dll`.

| Target | SHA-256 |
| --- | --- |
| KK | `496138652fff624638395d947bf60c1ca2b72f530a9c9d494e1804a9f2b1b686` |
| KKS | `6554f04badf59acf97441f3070b6a119c6f31b0eb3e984e5baa39b87a7f94a17` |
