#!/usr/bin/env python3
"""Structural validation only. This does not replace Unity import/compilation."""
import json
import re
import sys
import uuid
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
ASSETS = ROOT / "Assets"
errors = []


def check(condition, message):
    if not condition:
        errors.append(message)


def read_json(path):
    try:
        return json.loads(path.read_text(encoding="utf-8"))
    except (ValueError, OSError) as error:
        errors.append(f"{path.relative_to(ROOT)}: {error}")
        return {}


def main():
    manifest = read_json(ROOT / "Packages/manifest.json")
    dependencies = manifest.get("dependencies", {})
    expected = {
        "com.unity.cinemachine": "3.1.3",
        "com.unity.inputsystem": "1.11.2",
        "com.unity.render-pipelines.universal": "17.0.4",
        "com.unity.ugui": "2.0.0",
        "com.unity.test-framework": "1.4.5",
    }
    for name, version in expected.items():
        check(dependencies.get(name) == version, f"Unexpected package version: {name}")
    check("com.unity.textmeshpro" not in dependencies, "TMP is included in uGUI 2; do not install the old TMP package.")
    settings = (ROOT / "ProjectSettings/ProjectSettings.asset").read_text()
    check(re.search(r"^  activeInputHandler: 1$", settings, re.M), "New Input System must be active.")

    guids = {}
    all_assets = [p for p in ASSETS.rglob("*") if p.suffix != ".meta"]
    for path in all_assets:
        meta = Path(str(path) + ".meta")
        check(meta.is_file(), f"Missing metadata: {path.relative_to(ROOT)}")
        if not meta.is_file():
            continue
        match = re.search(r"^guid: ([0-9a-f]{32})$", meta.read_text(), re.M)
        check(match is not None, f"Invalid GUID in {meta.relative_to(ROOT)}")
        if match:
            key = match.group(1)
            check(key not in guids, f"Duplicate GUID: {meta.relative_to(ROOT)}")
            guids[key] = path
    for meta in ASSETS.rglob("*.meta"):
        check(Path(str(meta)[:-5]).exists(), f"Orphan metadata: {meta.relative_to(ROOT)}")
    external = {"8404be70184654265930450def6a9037"}  # Input System 1.11.2's InputActionImporter.
    for path in ASSETS.rglob("*"):
        if not path.is_file() or path.suffix not in {".meta", ".asset", ".unity"}:
            continue
        for key in re.findall(r"guid: ([0-9a-f]{32})", path.read_text()):
            check(key in guids or key in external, f"Broken GUID reference in {path.relative_to(ROOT)}: {key}")

    assemblies = {}
    for path in ASSETS.rglob("*.asmdef"):
        data = read_json(path)
        name = data.get("name")
        check(name not in assemblies, f"Duplicate assembly {name}")
        assemblies[name] = data
    for name, data in assemblies.items():
        for reference in data.get("references", []):
            check(reference in assemblies or reference.startswith("Unity."), f"Unknown assembly reference {reference} in {name}")
    check(assemblies.get("Wynnyng.Core", {}).get("noEngineReferences") is True, "Core must not reference the engine.")
    visited, visiting = set(), set()

    def visit(name):
        check(name not in visiting, f"Assembly cycle involving {name}")
        if name in visited or name in visiting:
            return
        visiting.add(name)
        for reference in assemblies[name].get("references", []):
            if reference in assemblies:
                visit(reference)
        visiting.remove(name)
        visited.add(name)

    for name in assemblies:
        visit(name)
    for path in ASSETS.rglob("*.cs"):
        parent = path.parent
        while parent != ROOT and not list(parent.glob("*.asmdef")):
            parent = parent.parent
        check(parent != ROOT, f"Code outside an assembly: {path.relative_to(ROOT)}")
        text = path.read_text()
        for expression in [r"\bUnityEngine\.Input\b", r"\bFindObjectOfType\b", r"\bFindObjectsOfType\b", r"\bSystem\.Linq\b", r"\bTODO\b"]:
            check(not re.search(expression, text), f"Forbidden API or unfinished code in {path.relative_to(ROOT)}: {expression}")
        if "Core" in path.relative_to(ASSETS).parts:
            check(not re.search(r"\b(UnityEngine|UnityEditor)\b", text), f"Engine dependency in Core: {path.name}")

    data = read_json(ASSETS / "Wynnyng/Resources/Input/Match.inputactions")
    ids = set()
    for action_map in data.get("maps", []):
        names = {a["name"] for a in action_map["actions"]}
        for item in [action_map] + action_map["actions"] + action_map["bindings"]:
            key = item["id"]
            check(key not in ids, f"Duplicate input ID {key}")
            ids.add(key)
            try:
                uuid.UUID(key)
            except ValueError:
                errors.append(f"Invalid input ID {key}")
        for binding in action_map["bindings"]:
            check(binding["action"] in names, f"Binding has no action: {binding}")
            check(binding["groups"] in {"Gamepad", "Keyboard"}, "Missing control-scheme group")
    check({m["name"] for m in data.get("maps", [])} == {"Gameplay", "Laboratory"}, "Missing action maps")
    if errors:
        for error in errors:
            print("FAIL:", error, file=sys.stderr)
        return 1
    print(f"PASS: {len(guids)} unique asset/folder GUIDs, {len(assemblies)} acyclic assemblies, input IDs and project settings.")
    print("Unity import, C# engine compilation, shader compilation and PlayMode are separate acceptance gates.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
