"""Build GIN Rope This for Koikatsu (KK) or Koikatsu Sunshine (KKS)."""
from pathlib import Path
import argparse
import os
import subprocess
import zipfile

ROOT = Path(__file__).resolve().parent
SOURCES = ["RopeCore.cs", "RopePlugin.cs", "RopeIntegration.cs",
           "RopeInterface.cs", "RopeSelectionUi.cs", "RopeAutoRig.cs", "RopeMeshSlices.cs"]


def run(arguments):
    result = subprocess.run([str(value) for value in arguments], cwd=ROOT,
                            stdout=subprocess.PIPE, stderr=subprocess.STDOUT,
                            encoding="utf-8", errors="replace",
                            creationflags=getattr(subprocess, "CREATE_NO_WINDOW", 0))
    print(result.stdout, end="", flush=True)
    if result.returncode:
        raise SystemExit(result.returncode)


def reference(plugin_root, name, preferred):
    candidate = plugin_root / preferred
    if candidate.is_file():
        return candidate
    candidates = list(plugin_root.rglob(name))
    if len(candidates) != 1:
        raise SystemExit("Expected one installed " + name + "; found "
                         + str(len(candidates)) + ". Check for missing or duplicate dependencies.")
    return candidates[0]


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--target", choices=["kk", "kks"], default="kk")
    parser.add_argument("--game-dir", type=Path)
    parser.add_argument("--compiler", type=Path)
    parser.add_argument("--tests-only", action="store_true")
    args = parser.parse_args()
    kks = args.target == "kks"
    framework = "v4.0.30319" if kks else "v3.5"
    compiler = args.compiler or (Path(os.environ.get("WINDIR", "C:/Windows"))
                                 / "Microsoft.NET/Framework" / framework / "csc.exe")
    if not compiler.is_file():
        raise SystemExit("Install the required .NET Framework compiler or supply --compiler.")
    output = ROOT / "build" / args.target
    binary = output / "bin"
    binary.mkdir(parents=True, exist_ok=True)
    tests = binary / "RopeCoreTests.exe"
    run([compiler, "/nologo", "/utf8output", "/target:exe", "/out:" + str(tests),
         ROOT / "src/RopeCore.cs", ROOT / "tests/RopeCoreTests.cs"])
    run([tests])
    mesh_tests = binary / "RopeMeshSlicesTests.exe"
    mesh_arguments = [compiler, "/nologo", "/utf8output", "/target:exe", "/out:" + str(mesh_tests)]
    if kks:
        mesh_arguments.append("/define:KKS")
    run(mesh_arguments + [ROOT / "src/RopeMeshSlices.cs",
                         ROOT / "tests/UnityMeshTestStubs.cs",
                         ROOT / "tests/RopeMeshSlicesTests.cs"])
    run([mesh_tests])
    if args.tests_only:
        return
    game_arg = args.game_dir or os.environ.get("KOIKATSU_SUNSHINE_DIR" if kks else "KOIKATSU_DIR")
    if not game_arg:
        parser.error("Supply --game-dir or set the environment variable for the target game.")
    game = Path(game_arg).expanduser().resolve()
    if not (game / "CharaStudio.exe").is_file():
        parser.error("The game directory must contain CharaStudio.exe.")
    names = ["CharaStudio_Data", "KoikatsuSunshine_Data"] if kks else ["CharaStudio_Data", "Koikatu_Data"]
    managed = next((game / name / "Managed" for name in names
                    if (game / name / "Managed/UnityEngine.dll").is_file()), None)
    if managed is None:
        raise SystemExit("Could not find the target game's managed assemblies.")
    is_kks_game = (managed / "UnityEngine.CoreModule.dll").is_file()
    if is_kks_game != kks:
        raise SystemExit("The game directory does not match --target. KK and KKS require separate builds.")
    plugins = game / "BepInEx/plugins"
    api = "KKSAPI.dll" if kks else "KKAPI.dll"
    save = "KKS_ExtensibleSaveFormat.dll" if kks else "ExtensibleSaveFormat.dll"
    save_folder = "KKS_BepisPlugins" if kks else "KK_BepisPlugins"
    references = [game / "BepInEx/core/BepInEx.dll",
                  reference(plugins, api, api),
                  reference(plugins, save, save_folder + "/" + save)]
    unity = sorted(managed.glob("UnityEngine*.dll")) if kks else [managed / "UnityEngine.dll", managed / "UnityEngine.UI.dll"]
    references += unity + [managed / "Assembly-CSharp.dll", managed / "Assembly-CSharp-firstpass.dll"]
    for name in (["UniRx.dll", "UniTask.dll"] if kks else []):
        if (managed / name).is_file():
            references.append(managed / name)
    for path in references:
        if not path.is_file():
            raise SystemExit("Missing local reference: " + str(path))
    dll = binary / "GIN Rope This.dll"
    arguments = [compiler, "/nologo", "/utf8output", "/target:library", "/platform:anycpu",
                 "/optimize+", "/out:" + str(dll),
                 "/resource:" + str(ROOT / "assets/RopeThis.png") + ",GINRope.Resources.RopeThis.png"]
    if kks:
        arguments.append("/define:KKS")
    run(arguments + ["/reference:" + str(path) for path in references]
        + [ROOT / "src" / name for name in SOURCES])
    name = "GIN_Rope_This_KKS.zip" if kks else "GIN_Rope_This.zip"
    package = output / name
    with zipfile.ZipFile(package, "w", zipfile.ZIP_DEFLATED) as archive:
        archive.write(dll, "BepInEx/plugins/GIN Rope This.dll")
    print("Built " + str(package.relative_to(ROOT)))


if __name__ == "__main__":
    main()
