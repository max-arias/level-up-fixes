from __future__ import annotations

import json
import re
import shutil
import struct
import tempfile
import zipfile
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
CONTENT = ROOT / "ThunderstoreContent"
DLL = ROOT / "build" / "LevelUpChoicesFixes.dll"
ARCHIVE = ROOT / "build" / "TeamTayne-LevelUpChoicesFixes-1.0.0.zip"


def validate_manifest():
    manifest_path = CONTENT / "manifest.json"
    data = json.loads(manifest_path.read_text(encoding="utf-8"))
    assert re.fullmatch(r"[A-Za-z0-9_]{1,128}", data["name"])
    assert re.fullmatch(r"\d+\.\d+\.\d+", data["version_number"])
    assert len(data["description"]) <= 250
    assert data["website_url"].startswith("https://")
    assert data["dependencies"] == ["karaeren-LevelUpChoices-1.1.3"]
    return data


def validate_icon():
    raw = (CONTENT / "icon.png").read_bytes()
    assert raw[:8] == b"\x89PNG\r\n\x1a\n"
    width, height = struct.unpack(">II", raw[16:24])
    assert (width, height) == (256, 256)


def package():
    validate_manifest()
    validate_icon()
    if not DLL.is_file():
        raise SystemExit(f"missing built assembly: {DLL}; build the project before packaging")
    if ARCHIVE.exists():
        ARCHIVE.unlink()
    files = {
        "icon.png": CONTENT / "icon.png",
        "README.md": CONTENT / "README.md",
        "manifest.json": CONTENT / "manifest.json",
        "CHANGELOG.md": CONTENT / "CHANGELOG.md",
        "BepInEx/plugins/LevelUpChoicesFixes/LevelUpChoicesFixes.dll": DLL,
    }
    with zipfile.ZipFile(ARCHIVE, "w", compression=zipfile.ZIP_DEFLATED) as archive:
        for name, path in files.items():
            archive.write(path, name)
    with zipfile.ZipFile(ARCHIVE) as archive:
        names = set(archive.namelist())
        assert {"icon.png", "README.md", "manifest.json"} <= names
        assert "LevelUpChoices.dll" not in " ".join(names)
        assert "ItemQualities.dll" not in " ".join(names)
        with tempfile.TemporaryDirectory(prefix="levelupchoicesfixes-profile-") as profile:
            archive.extractall(profile)
            installed = Path(profile) / "BepInEx/plugins/LevelUpChoicesFixes/LevelUpChoicesFixes.dll"
            assert installed.read_bytes() == DLL.read_bytes()
    print(ARCHIVE)


if __name__ == "__main__":
    package()
