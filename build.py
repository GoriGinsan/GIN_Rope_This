"""Build GIN Rope This using a local Koikatsu installation."""
from pathlib import Path
import argparse
import os
import subprocess
import sys
import zipfile

ROOT = Path(__file__).resolve().parent
SOURCES = [
    "RopeCore.cs", "RopePlugin.cs", "RopeIntegration.cs",
    "RopeInterface.cs", "RopeSelectionUi.cs", "RopeAutoRig.cs",
]


def run(arguments):
    result = subprocess.run(
        [str(value) for value in arguments], cwd=ROOT,
        stdout=subprocess.PIPE, stderr=subprocess.STDOUT,
        encoding="utf-8", errors="replace",
        creationflags=getattr(subprocess, "CREATE_NO_WINDOW", 0),
    )
    print(result.stdout, end="")
    if result.returncode:
        raise SystemExit(result.returncode)


def reference(plugin_root, name, preferred):
    candidate = plugin_root / preferred
    if candidate.is_file():
        return candidate
    candidates = list(plugin_root.rglob(name))
    if len(candidates) != 1:
        raise SystemExit(
            "Expected one installed " + name + "; found " + str(len(candidates))
            + ". Check the dependency installation and remove duplicate copies."
        )
    return candidates[0]


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--game-dir", default=os.environ.get("KOIKATSU_DIR"))
    parser.add_argument("--compiler", type=Path)
    parser.add_argument("--tests-only", action="store_true")
    args = parser.parse_args()
    compiler = args.compiler or (
        Path(os.environ.get("WINDIR", "C:/Windows"))
        / "Microsoft.NET/Framework/v3.5/csc.exe"
    )
    if not compiler.is_file():
        raise SystemExit("Install .NET Framework 3.5 or supply --compiler.")
    output = ROOT / "build"
    binary = output / "bin"
    binary.mkdir(parents=True, exist_ok=True)
    tests = binary / "RopeCoreTests.exe"
    run([compiler, "/nologo", "/utf8output", "/target:exe", "/out:" + str(tests),
         ROOT / "src/RopeCore.cs", ROOT / "tests/RopeCoreTests.cs"])
    run([tests])
    if args.tests_only:
        return
    if not args.game_dir:
        parser.error("Supply --game-dir or set KOIKATSU_DIR.")
    game = Path(args.game_dir).expanduser().resolve()
    if not (game / "CharaStudio.exe").is_file():
        parser.error("The game directory must contain CharaStudio.exe.")
    managed = next((game / name / "Managed" for name in
                    ["CharaStudio_Data", "Koikatu_Data"]
                    if (game / name / "Managed/UnityEngine.dll").is_file()), None)
    if managed is None:
        raise SystemExit("Could not find the game's managed Unity assemblies.")
    plugins = game / "BepInEx/plugins"
    references = [
        game / "BepInEx/core/BepInEx.dll",
        reference(plugins, "KKAPI.dll", "KKAPI.dll"),
        reference(plugins, "ExtensibleSaveFormat.dll", "KK_BepisPlugins/ExtensibleSaveFormat.dll"),
    ] + [managed / name for name in [
        "UnityEngine.dll", "UnityEngine.UI.dll", "Assembly-CSharp.dll",
        "Assembly-CSharp-firstpass.dll",
    ]]
    for path in references:
        if not path.is_file():
            raise SystemExit("Missing local reference: " + str(path))
    dll = binary / "GIN Rope This.dll"
    icon = ROOT / "assets/RopeThis.png"
    run([compiler, "/nologo", "/utf8output", "/target:library", "/platform:anycpu",
         "/optimize+", "/out:" + str(dll),
         "/resource:" + str(icon) + ",GINRope.Resources.RopeThis.png"]
        + ["/reference:" + str(path) for path in references]
        + [ROOT / "src" / name for name in SOURCES])
    package = output / "GIN_Rope_This.zip"
    with zipfile.ZipFile(package, "w", zipfile.ZIP_DEFLATED) as archive:
        archive.write(dll, "BepInEx/plugins/GIN Rope This.dll")
    print("Built " + str(package.relative_to(ROOT)))


if __name__ == "__main__":
    main()
