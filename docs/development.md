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

Run only the standalone solver and scene-codec suite without a game:

```powershell
python build.py --tests-only
python build.py --target kks --tests-only
```

## File map

| File | Purpose |
| --- | --- |
| `src/RopePlugin.cs` | Lifecycle, handles, physics application, and scene integration |
| `src/RopeCore.cs` | Game-independent solver, settings, bake data, and scene codec |
| `src/RopeSelectionUi.cs` | Main controls, endpoint selection, and automatic rig controls |
| `src/RopeAutoRig.cs` | Reversible mesh copies, generated bones, skinning, and renderer state |
| `src/RopeInterface.cs` | Endpoint suggestions, markers, and compatibility helpers |
| `src/RopeIntegration.cs` | Optional integration and legacy saved-data support |
| `tests/RopeCoreTests.cs` | Standalone solver and codec tests |

The `KKS` compile symbol selects the KKS startup label, the newer skin-quality API, and a guard against unsupported variable skin weights. Unity 2019 engine modules are referenced only by the KKS build. The source retains older saved-data support; connection and animation tabs remain hidden.

## Identity and resources

Keep the GUID `local.kkworkshop.0a52b20541f5.gin-rope-this` for both targets when updating this plugin. Configuration and plugin scene-data identity stay consistent within each game. This does not guarantee cross-game scene-file conversion.

The build embeds `assets/RopeThis.png` as `GINRope.Resources.RopeThis.png`. Only the plugin DLL is required after installation.

## Runtime checks

Use isolated KK and KKS installations for UI, rendering, NodesConstraints, duplication, save/load, and restart checks. A successful compilation alone does not prove runtime compatibility. Follow the [tested scope and checklist](testing.md).
