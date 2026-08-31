#!/usr/bin/env python3
"""Builds release artifacts for BTCantinaMissions.

The DLL is modpack-agnostic; job/reward definitions live in per-modpack folders
under packs/. This script builds the DLL once and assembles one self-contained
drop-in zip per pack: <Pack>/mod.json + jobs/ + rewards/ + the DLL.

Usage:
    python package.py                  # package every pack under packs/
    python package.py RT               # package selected packs only

Output: dist/BTCantinaMissions-<Pack>-v<version>.zip (version from the pack's
mod.json; all packs and the DLL are versioned in lockstep — a mismatch between
the pack manifest and src/Core.cs AssemblyVersion is reported as an error).
"""

import json
import pathlib
import re
import shutil
import subprocess
import sys
import zipfile

ROOT = pathlib.Path(__file__).resolve().parent
DIST = ROOT / "dist"
DLL = ROOT / "bin" / "Release" / "net472" / "BTCantinaMissions.dll"


def assembly_version() -> str:
    core = (ROOT / "src" / "Core.cs").read_text(encoding="utf-8-sig")
    m = re.search(r'AssemblyVersion\("([^"]+)"\)', core)
    if not m:
        raise SystemExit("cannot read AssemblyVersion from src/Core.cs")
    return m.group(1)


def package(pack: str, version: str) -> pathlib.Path:
    pack_dir = ROOT / "packs" / pack
    if not pack_dir.is_dir():
        raise SystemExit(f"pack folder not found: {pack_dir}")

    mod_json = json.loads((pack_dir / "mod.json").read_text(encoding="utf-8-sig"))
    pack_version = mod_json.get("Version")
    if pack_version != version:
        raise SystemExit(
            f"version mismatch for {pack}: mod.json={pack_version}, AssemblyVersion={version} "
            f"(packs are versioned in lockstep — bump both)"
        )

    DIST.mkdir(exist_ok=True)
    zip_path = DIST / f"BTCantinaMissions-{pack}-v{version}.zip"
    with zipfile.ZipFile(zip_path, "w", zipfile.ZIP_DEFLATED) as zf:
        # the zip contains the mod folder itself, so it extracts into Mods/
        zf.writestr("BTCantinaMissions/mod.json", (pack_dir / "mod.json").read_text(encoding="utf-8-sig"))
        zf.write(DLL, "BTCantinaMissions/BTCantinaMissions.dll")
        settings = pack_dir / "settings.json"
        if settings.is_file():
            zf.write(settings, "BTCantinaMissions/settings.json")
        install = pack_dir / "INSTALLATION.md"
        if install.is_file():
            zf.write(install, "BTCantinaMissions/INSTALLATION.md")
        for pattern in ("jobs/**/*.json", "rewards/**/*.csv"):
            for f in pack_dir.glob(pattern):
                zf.write(f, f"BTCantinaMissions/{f.relative_to(pack_dir).as_posix()}")
    return zip_path


def main() -> None:
    packs = sys.argv[1:] or sorted(
        d.name for d in (ROOT / "packs").iterdir() if (d / "mod.json").is_file()
    )
    print("building DLL (Release)...")
    subprocess.run(["dotnet", "build", str(ROOT / "BTCantinaMissions.csproj"), "-c", "Release"],
                   cwd=ROOT, check=True)
    if not DLL.exists():
        raise SystemExit(f"DLL not found after build: {DLL}")

    version = assembly_version()
    for pack in packs:
        zip_path = package(pack, version)
        print(f"packaged {pack}: {zip_path.name}")


if __name__ == "__main__":
    main()
