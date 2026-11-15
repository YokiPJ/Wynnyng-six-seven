import json
import unittest

from validate_project import ROOT, builtin_module_errors, serialized_yaml_errors, tag_manager_errors


class BuiltinModuleTests(unittest.TestCase):
    def test_original_misspelling_is_rejected(self):
        errors = builtin_module_errors({"com.unity.modules.screen-capture": "1.0.0"})
        self.assertEqual(len(errors), 1)
        self.assertIn("com.unity.modules.screen-capture", errors[0])

    def test_documented_screen_capture_name_is_accepted(self):
        self.assertEqual(builtin_module_errors({"com.unity.modules.screencapture": "1.0.0"}), [])

    def test_invalid_builtin_version_is_rejected(self):
        self.assertTrue(builtin_module_errors({"com.unity.modules.screencapture": "9.0.0"}))

    def test_shipped_manifest_has_valid_builtin_names_and_versions(self):
        manifest = json.loads((ROOT / "Packages/manifest.json").read_text())
        self.assertEqual(builtin_module_errors(manifest["dependencies"]), [])


class SerializedYamlTests(unittest.TestCase):
    def test_bare_sequence_marker_from_original_file_is_rejected(self):
        problems = serialized_yaml_errors("layers:\n  - Default\n  -\nnext: value\n")
        self.assertTrue(any("bare '-'" in problem for problem in problems))

    def test_explicit_empty_string_is_valid(self):
        self.assertEqual(serialized_yaml_errors('layers:\n  - Default\n  - ""\n'), [])

    def test_malformed_mapping_reports_a_syntax_error(self):
        self.assertTrue(serialized_yaml_errors("settings: {missing: 1\n"))

    def test_duplicate_mapping_keys_are_rejected(self):
        problems = serialized_yaml_errors("guid: first\nguid: second\n")
        self.assertTrue(any("duplicate key" in problem for problem in problems))

    def test_multi_document_unity_scene_tags_and_stripped_marker_are_supported(self):
        source = ("%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n"
                  "--- !u!1 &1000\nGameObject:\n  m_Name: Root\n"
                  "--- !u!4 &1001 stripped\nTransform:\n  m_GameObject: {fileID: 1000}\n")
        self.assertEqual(serialized_yaml_errors(source), [])

    def test_shipped_tag_manager_has_32_string_slots_and_expected_layers(self):
        source = (ROOT / "ProjectSettings/TagManager.asset").read_text()
        self.assertEqual(tag_manager_errors(source), [])

    def test_null_layer_is_not_an_empty_string(self):
        source = (ROOT / "ProjectSettings/TagManager.asset").read_text().replace('  - ""', '  - null', 1)
        self.assertTrue(any("Layer 3 must be a string" in problem for problem in tag_manager_errors(source)))

    def test_wrong_layer_index_is_rejected(self):
        source = (ROOT / "ProjectSettings/TagManager.asset").read_text().replace("  - Ground", "  - WrongGround", 1)
        self.assertTrue(any("Layer 8 must be Ground" in problem for problem in tag_manager_errors(source)))

    def test_missing_layer_slot_is_rejected(self):
        source = (ROOT / "ProjectSettings/TagManager.asset").read_text().replace('  - ""\n', '', 1)
        self.assertTrue(any("exactly 32" in problem for problem in tag_manager_errors(source)))


if __name__ == "__main__":
    unittest.main()
