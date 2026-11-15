import json
import unittest

from validate_project import ROOT, builtin_module_errors


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


if __name__ == "__main__":
    unittest.main()
