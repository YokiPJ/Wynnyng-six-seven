#!/usr/bin/env python3
"""Structural validation only. This does not replace Unity import/compilation."""
import json
import re
import sys
import uuid
from pathlib import Path

try:
    import yaml
except ImportError:
    yaml = None

ROOT = Path(__file__).resolve().parents[1]
ASSETS = ROOT / "Assets"
errors = []

# Names checked against the Unity 6 manual. Extend this set only after checking
# the corresponding com.unity.modules.<name> documentation for this Editor.
BUILTIN_MODULES = frozenset("com.unity.modules." + name for name in (
    "animation", "assetbundle", "audio", "director", "imageconversion",
    "imgui", "jsonserialize", "particlesystem", "physics", "physics2d",
    "screencapture", "terrain", "terrainphysics", "ui", "uielements",
    "unitywebrequest", "unitywebrequestassetbundle", "unitywebrequestaudio",
    "unitywebrequesttexture", "vehicles", "video", "vr", "wind", "xr",
))


if yaml is not None:
    class UniqueKeyLoader(yaml.SafeLoader):
        """Reject ambiguous mappings that SafeLoader otherwise accepts by overwriting keys."""

        def construct_mapping(self, node, deep=False):
            mapping = {}
            for key_node, value_node in node.value:
                key = self.construct_object(key_node, deep=deep)
                if key in mapping:
                    raise yaml.constructor.ConstructorError(
                        "while reading a mapping", node.start_mark,
                        f"duplicate key: {key}", key_node.start_mark)
                mapping[key] = self.construct_object(value_node, deep=deep)
            return mapping


def serialized_yaml_errors(text):
    """Check YAML syntax after removing Unity document tags, not Unity's native parser."""
    if yaml is None:
        return ["Install validation dependencies: python3 -m pip install -r tools/requirements-validation.txt"]
    problems = []
    for line_number, line in enumerate(text.splitlines(), 1):
        if re.fullmatch(r"\s*-", line):
            problems.append(
                f"line {line_number}: bare '-' sequence item; serialize an explicit value such as \"\"")
    # Unity applies !u! to every document although a YAML %TAG directive only
    # covers its first document. Strip that document-header extension and its
    # optional prefab marker, leaving line numbers/content intact for diagnostics.
    normalized = re.sub(r"(?m)^--- !u!\d+ (&-?\d+)(?: stripped)?[ \t]*$", r"--- \1", text)
    try:
        list(yaml.load_all(normalized, Loader=UniqueKeyLoader))
    except yaml.YAMLError as error:
        mark = getattr(error, "problem_mark", None)
        location = f"line {mark.line + 1}: " if mark is not None else ""
        problems.append(location + str(getattr(error, "problem", error)))
    return problems


def tag_manager_errors(text):
    """Check the layer contract used by WorldLayers before Unity imports it."""
    problems = serialized_yaml_errors(text)
    if problems:
        return problems
    normalized = re.sub(r"(?m)^--- !u!\d+ (&-?\d+)(?: stripped)?[ \t]*$", r"--- \1", text)
    documents = list(yaml.load_all(normalized, Loader=UniqueKeyLoader))
    if len(documents) != 1 or not isinstance(documents[0], dict):
        return ["TagManager must contain exactly one mapping document"]
    manager = documents[0].get("TagManager")
    if not isinstance(manager, dict):
        return ["Missing TagManager mapping"]
    layers = manager.get("layers")
    if not isinstance(layers, list) or len(layers) != 32:
        problems.append("TagManager.layers must contain exactly 32 entries")
    if isinstance(layers, list):
        for index, name in enumerate(layers):
            if not isinstance(name, str):
                problems.append(f"Layer {index} must be a string; use \"\" for an unused slot")
        required = {0: "Default", 1: "TransparentFX", 2: "Ignore Raycast", 4: "Water", 5: "UI",
                    8: "Ground", 9: "Goal", 10: "Ball"}
        for index, name in required.items():
            if index >= len(layers) or layers[index] != name:
                problems.append(f"Layer {index} must be {name}")
    tags = manager.get("tags")
    if not isinstance(tags, list) or any(not isinstance(tag, str) for tag in tags):
        problems.append("TagManager.tags must be an array of strings")
    sorting = manager.get("m_SortingLayers")
    if not isinstance(sorting, list) or not any(
            isinstance(item, dict) and item.get("name") == "Default" and item.get("uniqueID") == 0
            for item in sorting):
        problems.append("Default sorting layer with uniqueID 0 is missing")
    rendering = manager.get("m_RenderingLayers")
    if not isinstance(rendering, list) or not rendering or rendering[0] != "Default" or any(
            not isinstance(name, str) for name in rendering):
        problems.append("Rendering layers must be strings, starting with Default")
    return problems


def builtin_module_errors(dependencies):
    problems = []
    for name, version in dependencies.items():
        if not name.startswith("com.unity.modules."):
            continue
        if name not in BUILTIN_MODULES:
            problems.append(f"Unknown or unverified Unity 6 built-in module: {name}")
        if version != "1.0.0":
            problems.append(f"Unexpected built-in module version: {name}@{version}")
    return problems


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
    errors.clear()
    if yaml is None:
        print("FAIL: Install validation dependencies: python3 -m pip install -r tools/requirements-validation.txt", file=sys.stderr)
        return 1
    manifest = read_json(ROOT / "Packages/manifest.json")
    dependencies = manifest.get("dependencies", {})
    errors.extend(builtin_module_errors(dependencies))
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

    yaml_files = [p for folder in (ASSETS, ROOT / "ProjectSettings") for p in folder.rglob("*")
                  if p.is_file() and p.suffix in {".meta", ".asset", ".unity", ".prefab", ".mat"}]
    checked_yaml = 0
    for path in yaml_files:
        data = path.read_bytes()
        # Native binary .asset files are not YAML; .meta files are always text.
        text_header = data[3:] if data.startswith(b"\xef\xbb\xbf") else data
        if path.suffix != ".meta" and not text_header.startswith(b"%YAML"):
            continue
        try:
            source = data.decode("utf-8-sig")
        except UnicodeDecodeError as error:
            errors.append(f"{path.relative_to(ROOT)}: invalid UTF-8 YAML: {error}")
            continue
        checked_yaml += 1
        errors.extend(f"{path.relative_to(ROOT)}: {problem}" for problem in serialized_yaml_errors(source))
    tag_manager = ROOT / "ProjectSettings/TagManager.asset"
    errors.extend(f"ProjectSettings/TagManager.asset: {problem}"
                  for problem in tag_manager_errors(tag_manager.read_text(encoding="utf-8-sig")))

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
    print(f"PASS: {len(guids)} unique asset/folder GUIDs, {len(assemblies)} acyclic assemblies, "
          f"{checked_yaml} YAML files, built-in modules, input IDs and project settings.")
    print("Unity import, C# engine compilation, shader compilation and PlayMode are separate acceptance gates.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
