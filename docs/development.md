# Development

## Build locally

The plugin targets **Unity 5.6.2f1**, **.NET Framework 3.5**, and **BepInEx 5**. Build it on Windows with Python 3 and the .NET Framework 3.5 C# compiler installed. Do not retarget the code to a modern Unity or IL2CPP template.

The game and dependency assemblies are local references only. They are not redistributed here.

1. Download the repository with **Code → Download ZIP**, or clone it.
2. Install BepInEx 5, KKAPI, and ExtensibleSaveFormat in a Koikatsu game installation.
3. Run:

```powershell
python build.py --game-dir "D:\Games\Koikatsu"
```

Alternatively, set `KOIKATSU_DIR` and run `python build.py`. Use `--compiler` to specify a different compatible compiler path.

The script compiles and runs the standalone tests, builds the plugin, and creates:

```text
build/
  bin/GIN Rope This.dll
  GIN_Rope_This.zip
```

The installation ZIP contains only `BepInEx/plugins/GIN Rope This.dll`. It does not bundle game assemblies or third-party plugins. The public release ZIP uses the runtime-tested DLL; a local rebuild may differ byte-for-byte because the legacy compiler does not produce deterministic binaries.

Run only the standalone tests with:

```powershell
python build.py --tests-only
```

No game installation is needed for those tests.

## File map

| File | Purpose |
| --- | --- |
| `src/RopePlugin.cs` | Plugin lifecycle, physics application, handles, and scene integration |
| `src/RopeCore.cs` | Solver, settings, bake data, and scene codec |
| `src/RopeSelectionUi.cs` | Main controls, endpoint selection, and automatic-rig controls |
| `src/RopeAutoRig.cs` | Reversible mesh copies, generated bones, skin weights, and renderer state |
| `src/RopeInterface.cs` | Endpoint suggestions, markers, and compatibility helpers |
| `src/RopeIntegration.cs` | Optional integration and legacy scene-compatibility support |
| `tests/RopeCoreTests.cs` | Standalone solver and codec tests |

The source retains compatibility code for older saved data. Connection and animation tabs are not exposed in the current panel.

## Identity and resources

Keep the plugin GUID `local.kkworkshop.0a52b20541f5.gin-rope-this` when updating this plugin. Changing it would separate the new build from existing scene data and configuration.

The build embeds `assets/RopeThis.png` as `GINRope.Resources.RopeThis.png`. The game needs only the DLL after installation.

## Runtime checks

The standalone suite does not launch Studio. Use a separate test game installation for renderer, UI, NodesConstraints, save/load, and scene-duplication checks. Do not treat a successful compilation as proof of runtime compatibility.

Follow the [runtime checklist](testing.md) when changing transforms, skinning, scene metadata, or input behavior.
