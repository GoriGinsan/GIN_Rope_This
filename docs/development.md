# Development

## Targets

One source tree builds two separate plugins. Do not copy a KK DLL into KKS or the reverse.

| Target | Engine | Framework | API assembly | Save assembly |
| --- | --- | --- | --- | --- |
| `kk` | Unity 5.6.2f1 | .NET Framework 3.5 | `KKAPI.dll` | `ExtensibleSaveFormat.dll` |
| `kks` | Unity 2019.4.9f1 | .NET Framework 4.6 | `KKSAPI.dll` | `KKS_ExtensibleSaveFormat.dll` |

Build on Windows with Python 3 and the matching .NET Framework compiler. KKS uses the v4.0.30319 compiler; KK uses v3.5. The script references assemblies from the selected game locally. Game and third-party assemblies are not redistributed.

## Build locally

1. Download the repository with **Code → Download ZIP**, or clone it.
2. Install BepInEx 5, the game's API, and ExtensibleSaveFormat in each target game.
3. Build the desired target:

```powershell
python build.py --target kk --game-dir "D:\Games\Koikatsu"
python build.py --target kks --game-dir "D:\Games\Koikatsu Sunshine"
```

The default target is `kk`. Alternatively, set `KOIKATSU_DIR` for KK or `KOIKATSU_SUNSHINE_DIR` for KKS. Use `--compiler` to select a compatible compiler explicitly.

```text
build/
  kk/
    bin/GIN Rope This.dll
    GIN_Rope_This.zip
  kks/
    bin/GIN Rope This.dll
    GIN_Rope_This_KKS.zip
```

Each ZIP contains only `BepInEx/plugins/GIN Rope This.dll`. Build outputs are ignored by Git. Local rebuilds may differ byte-for-byte because these compilers do not emit deterministic binaries.

Run the standalone principal-axis, solver, scene-codec, and mesh subdivision suites without a game:

```powershell
python build.py --tests-only
python build.py --target kks --tests-only
```

## File map

| File | Purpose |
| --- | --- |
| `src/RopePlugin.cs` | Lifecycle, handles, physics application, and scene integration |
| `src/RopeCore.cs` | Game-independent principal-axis fit, solver, settings, bake data, and scene codec |
| `src/RopeSelectionUi.cs` | Main controls, endpoint selection, and automatic rig controls |
| `src/RopeAutoRig.cs` | Reversible mesh copies, generated bones, skinning, and renderer state |
| `src/RopeMeshSlices.cs` | Bounded surface subdivision along the fitted rest line |
| `src/RopeInterface.cs` | Endpoint suggestions, markers, and compatibility helpers |
| `src/RopeIntegration.cs` | Optional integration and legacy saved-data support |
| `tests/RopeCoreTests.cs` | Standalone solver and codec tests |
| `tests/RopeMeshSlicesTests.cs` | Subdivision geometry, attributes, seams, and rejection tests |
| `tests/UnityMeshTestStubs.cs` | Data-only Mesh substitutes used only by standalone subdivision tests |

The `KKS` compile symbol selects the KKS startup label, the newer skin-quality API, and a guard against unsupported variable skin weights. Unity 2019 engine modules are referenced only by the KKS build. The source retains older saved-data support; connection and animation tabs remain hidden.

## Identity and resources

Keep the GUID `local.kkworkshop.0a52b20541f5.gin-rope-this` for both targets when updating this plugin. Configuration and plugin scene-data identity stay consistent within each game. This does not guarantee cross-game scene-file conversion.

The build embeds `assets/RopeThis.png` as `GINRope.Resources.RopeThis.png`. Only the plugin DLL is required after installation.

## Runtime checks

Automatic generation fits a principal axis to visible, posed mesh vertices in physical units after removing the item root's rigid rotation. Endpoints use projected vertex extrema; transverse bounds center the line. Nearly equal principal dimensions use an item-relative Y, X, Z preference, with tolerance for Unity float transform noise. Highly irregular geometry or uneven vertex density can make the fitted direction differ from a user's preferred direction.

Scene schema 3 remains unchanged. Saved item-local endpoints always take precedence over fitting, so old world-X/Y ropes and new arbitrary-direction ropes keep their rest line after rotation, duplication, and reload.

Before skinning, long triangles in the private posed copy are clipped at quarter-bone-interval planes. Shared original edges share cut vertices; authored UV seams and submeshes remain separate. Current blend-shape deformation is already baked, so copied blend-shape arrays are cleared only when vertex counts change. Original asset meshes are never edited.

Rendering uses up to 128 separate skin frames while the requested physics-node count stays unchanged. Ordinary sparse meshes retain their actual axial vertex stations; more complex meshes use a bounded sample grid. A vertex follows one rigid frame instead of blending two rotation matrices, avoiding the cross-section collapse that linear blending can produce at strong bends. Frame rotation follows parallel-transported tangents, with a deterministic normal for opposite directions. Corrected bindposes retain the displayed nonuniform scale without inherited shear.

Subdivision is planned and bounded before any source renderer is suppressed. KK has a 65,535-vertex limit per generated mesh; KKS can use 32-bit indices. The item-wide 500,000-vertex limit still applies. Standalone Mesh substitutes test CPU planning and interpolation; native rendering, skinning, and scene restoration require the separate game checks.

Use isolated KK and KKS installations for UI, rendering, NodesConstraints, duplication, save/load, and restart checks. A successful compilation alone does not prove runtime compatibility. Follow the [tested scope and checklist](testing.md).
