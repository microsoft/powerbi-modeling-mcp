"""Offline regression tests; no accounts, network, or user config are used."""

import importlib.util
import json
import subprocess
import tempfile
import unittest
from pathlib import Path
from unittest.mock import patch

ROOT = Path(__file__).resolve().parents[1]


def module(name):
    spec = importlib.util.spec_from_file_location(name, ROOT / "scripts" / (name + ".py"))
    result = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(result)
    return result


setup = module("macos_setup")
fabric = module("fabric_request")


class SetupTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)

    def test_config_preserves_servers_and_is_idempotent(self):
        config = self.root / "mcp.json"
        original = {"inputs": [{"id": "existing"}], "servers": {"existing": {"type": "http", "url": "https://example.org/mcp"}}}
        config.write_text(json.dumps(original))
        server = setup.model_server(self.root / "path with spaces" / "server", "read-only")
        setup.register_server(config, server)
        first = config.read_bytes()
        setup.register_server(config, server)
        self.assertEqual(first, config.read_bytes())
        actual = json.loads(first)
        self.assertEqual(actual["inputs"], original["inputs"])
        self.assertEqual(actual["servers"]["existing"], original["servers"]["existing"])
        self.assertEqual(len(list(self.root.glob("*.bak"))), 1)
        self.assertNotIn("--accept-eula", actual["servers"][setup.SERVER_NAME]["args"])

    def test_config_conflict_and_jsonc_are_not_overwritten(self):
        config = self.root / "mcp.json"
        for contents in ('{"servers":{"powerbi-macos":{"command":"existing"}}}', '{/*comment*/"servers":{}}'):
            config.write_text(contents)
            with self.assertRaises(ValueError):
                setup.register_server(config, setup.model_server(self.root / "binary", "read-only"))
            self.assertEqual(config.read_text(), contents)

    def test_explicit_write_mode_requires_confirmation(self):
        self.assertEqual(setup.model_server(self.root / "binary", "read-write")["args"],
                         ["--read-write", "--require-confirmation"])

    def test_wrong_platform_stops_before_install(self):
        with patch.object(setup.platform, "system", return_value="Linux"), patch.object(setup, "run") as run:
            with self.assertRaises(ValueError):
                setup.preflight()
            run.assert_not_called()

    def test_patch_is_idempotent_and_retains_originals(self):
        (self.root / "preview").mkdir()
        for relative in ("cli.js", "preview/index.js"):
            (self.root / relative).write_text(setup.AUTH_CHECK)
        setup.patch_report_cli(self.root)
        setup.patch_report_cli(self.root)
        for relative in ("cli.js", "preview/index.js"):
            path = self.root / relative
            self.assertEqual(path.read_text().count(setup.PATCH_MARKER), 1)
            self.assertEqual(path.with_name(path.name + ".before-my-workspace.bak").read_text(), setup.AUTH_CHECK)

    def test_unexpected_second_entry_point_prevents_all_patch_writes(self):
        (self.root / "preview").mkdir()
        (self.root / "cli.js").write_text(setup.AUTH_CHECK)
        (self.root / "preview/index.js").write_text("unexpected implementation")
        with self.assertRaises(ValueError):
            setup.patch_report_cli(self.root)
        self.assertEqual((self.root / "cli.js").read_text(), setup.AUTH_CHECK)
        self.assertFalse(list(self.root.rglob("*.bak")))

    def test_existing_skill_is_preserved(self):
        source = self.root / "source"
        source.mkdir()
        (source / "SKILL.md").write_text("skill")
        destination = self.root / "destination"
        setup.register_skill(source, destination)
        setup.register_skill(source, destination)
        self.assertEqual(destination.resolve(), source.resolve())
        different = self.root / "different"
        different.mkdir()
        with self.assertRaises(ValueError):
            setup.register_skill(source, different)

    def test_node_version_requirement(self):
        self.assertEqual(setup.node_major("v24.11.1\n"), 24)
        with self.assertRaises(ValueError):
            setup.node_major("unrecognized")

    def test_token_spawn_uses_shell_only_on_windows(self):
        path = self.root / "cli.js"
        path.write_text(setup.TOKEN_SPAWN_OLD)
        setup.patch_token_spawn(path)
        setup.patch_token_spawn(path)
        self.assertEqual(path.read_text(), setup.TOKEN_SPAWN_NEW)
        self.assertEqual(path.with_name("cli.js.before-safe-token-spawn.bak").read_text(), setup.TOKEN_SPAWN_OLD)

    def test_mac_skill_lifecycle_keeps_source_clean_and_shared_references(self):
        checkout = self.root / "checkout"
        source = checkout / "skills/powerbi-report-cli"
        lifecycle = source / "references/authoring/preview-part-03.md"
        lifecycle.parent.mkdir(parents=True)
        lifecycle.write_text(setup.SCREENSHOT_WINDOWS)
        (source / "SKILL.md").write_text("test skill")
        (checkout / "common").mkdir()
        (checkout / "common/COMMON-CLI.md").write_text("shared reference")
        (checkout / "LICENSE").write_text("test license")
        destination = self.root / "package"
        target = setup.prepare_report_skill(checkout, destination)
        setup.prepare_report_skill(checkout, destination)
        self.assertEqual(lifecycle.read_text(), setup.SCREENSHOT_WINDOWS)
        self.assertIn(setup.SCREENSHOT_MAC, (target / "references/authoring/preview-part-03.md").read_text())
        self.assertTrue((target / "references/../../../common/COMMON-CLI.md").resolve().is_file())

    def test_wrapper_handles_spaces_and_preserves_tls_verification(self):
        path = self.root / "bin with spaces" / "author"
        setup.write_wrapper(path, Path("/test/node path"), Path("/test/cli path.js"))
        result = subprocess.run(["node", "--check", str(path)], capture_output=True, text=True)
        self.assertEqual(result.returncode, 0, result.stderr)
        self.assertIn("--use-system-ca", path.read_text())
        self.assertNotIn("NODE_TLS_REJECT_UNAUTHORIZED", path.read_text())
        self.assertIn("result.signal", path.read_text())

    def test_personal_workspace_fallback_does_not_hide_authorization_errors(self):
        logic = setup.PERSONAL_WORKSPACE_PATCH + 'code: "AUTH_REQUIRED"});\n  }\n  return response;'
        script = '''
import assert from 'node:assert/strict';
const AsyncFunction = Object.getPrototypeOf(async function(){}).constructor;
const logic = new AsyncFunction('response', 'fetch', 'datasetId', 'apiBaseUrl', 'PreviewError', 'token', LOGIC);
class PreviewError extends Error {}
const cases = [
  {status:200, body:{}, fallback:false, succeeds:true},
  {status:401, body:{error:{code:'GroupNotAccessible',message:'Calling group APIs not permitted for personal workspace'}}, fallback:true, succeeds:true},
  {status:403, body:{error:{code:'Forbidden',message:'No access'}}, fallback:false, succeeds:false},
  {status:401, body:{error:{code:'GroupNotAccessible',message:'Other group problem'}}, fallback:false, succeeds:false}
];
for (const example of cases) {
  const urls = [];
  const response = {status:example.status,clone:()=>({json:async()=>example.body})};
  const fetch = async url => {urls.push(url); return {status:200};};
  const operation = () => logic(response,fetch,'dataset-id',()=> 'https://api.powerbi.com',PreviewError,'fake-token');
  if (example.succeeds) assert.equal((await operation()).status,200);
  else await assert.rejects(operation,/Not authorized/);
  assert.equal(urls.length, example.fallback ? 1 : 0);
  if (urls.length) assert.equal(urls[0],'https://api.powerbi.com/v1.0/myorg/datasets/dataset-id');
}
'''.replace("LOGIC", json.dumps(logic))
        result = subprocess.run(["node", "--input-type=module", "-e", script], capture_output=True, text=True)
        self.assertEqual(result.returncode, 0, result.stderr)


class FabricTransportTests(unittest.TestCase):
    def test_rejects_untrusted_hosts_and_header_injection(self):
        for url in ("http://api.fabric.microsoft.com/v1/workspaces", "https://api.fabric.microsoft.com.evil.test/v1/x",
                    "https://user@api.fabric.microsoft.com/v1/x", "https://api.fabric.microsoft.com/v1/x\nheader: injected",
                    "https://api.fabric.microsoft.com:443/v1/x", "https://api.fabric.microsoft.com/v1/x#fragment"):
            with self.subTest(url=url), self.assertRaises(ValueError):
                fabric.validate_url(url)

    def test_remote_mutation_is_blocked_before_token_acquisition(self):
        with patch.object(fabric.subprocess, "run") as run:
            with self.assertRaises(ValueError):
                fabric.request("https://api.fabric.microsoft.com/v1/workspaces/test/reports/id/updateDefinition", "POST")
            run.assert_not_called()

    def test_get_definition_is_read_only(self):
        self.assertFalse(fabric.requires_write("POST", "https://api.fabric.microsoft.com/v1/workspaces/w/reports/r/getDefinition?format=PBIR"))
        self.assertTrue(fabric.requires_write("POST", "https://api.fabric.microsoft.com/v1/workspaces/w/reports/r/updateDefinition"))

    def test_curl_settings_keep_token_in_stdin(self):
        settings = fabric.curl_configuration("https://api.fabric.microsoft.com/v1/workspaces", "GET", "test-token")
        self.assertIn('header = "Authorization: Bearer test-token"', settings)
        self.assertIn("x-ms-fabric-skill", settings)
        with self.assertRaises(ValueError):
            fabric.curl_configuration("https://api.fabric.microsoft.com/v1/workspaces", "GET", "token\ninjected")

    def test_lro_headers_and_proxy_headers(self):
        raw = "HTTP/1.1 200 Connection established\r\n\r\nHTTP/2 202\r\nLocation: https://api.fabric.microsoft.com/v1/operations/id\r\nRetry-After: 5\r\nx-ms-operation-id: id\r\nSet-Cookie: do-not-save\r\n\r\n"
        headers = fabric.parse_headers(raw)
        self.assertEqual(headers["x-ms-operation-id"], "id")
        self.assertEqual(headers["retry-after"], "5")
        self.assertNotIn("set-cookie", headers)
        with self.assertRaises(ValueError):
            fabric.parse_headers("HTTP/2 202\nLocation: https://evil.test/v1/operations/id\n")

    def test_request_uses_verified_curl_and_private_stdin(self):
        commands = []

        def execute(command, **kwargs):
            commands.append(command)
            if command[0] == "az":
                self.assertIn("https://api.fabric.microsoft.com", command)
                return subprocess.CompletedProcess(command, 0, json.dumps({"accessToken": "fake-private-token"}), "")
            self.assertEqual(command[0], "curl")
            self.assertNotIn("--insecure", command)
            self.assertNotIn("-L", command)
            self.assertNotIn("fake-private-token", " ".join(command))
            self.assertIn("fake-private-token", kwargs["input"])
            Path(command[command.index("--dump-header") + 1]).write_text("HTTP/2 200\n\n")
            Path(command[command.index("--output") + 1]).write_text('{"displayName":"Test"}')
            return subprocess.CompletedProcess(command, 0, "200", "")

        with patch.object(fabric.subprocess, "run", side_effect=execute):
            response = fabric.request("https://api.fabric.microsoft.com/v1/workspaces/w/reports/r")
        self.assertEqual(response["status"], 200)
        self.assertEqual(response["body"]["displayName"], "Test")
        self.assertEqual(len(commands), 2)


if __name__ == "__main__":
    unittest.main()
