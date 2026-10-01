#!/usr/bin/env python3
"""Install a local Apple Silicon Power BI model and report authoring workflow.

Downloads are from Microsoft packages and repositories. No vendor binary is
redistributed, EULA accepted, tenant contacted, or report published by setup.
"""

import argparse
import hashlib
import json
import os
import platform
import re
import shutil
import subprocess
import sys
from pathlib import Path

MODEL_VERSION = "1.0.0"
REPORT_VERSION = "0.4.0"
SKILLS_COMMIT = "6c11ad58c25992e5d1435ce7cd80d217d5598a31"
SERVER_NAME = "powerbi-macos"
PATCH_MARKER = "// Local compatibility fix: personal workspaces reject the groups API."
AUTH_CHECK = '''  if (response.status === 401 || response.status === 403) {
    throw new PreviewError("Not authorized to read the dataset (check the token and dataset permissions).", {'''
PERSONAL_WORKSPACE_PATCH = '''  // Local compatibility fix: personal workspaces reject the groups API.
  // Fall back only for the explicit GroupNotAccessible service response.
  if (response.status === 401 || response.status === 403) {
    const rejected = await response.clone().json().catch(() => null);
    if (rejected?.error?.code === "GroupNotAccessible" &&
        rejected?.error?.message?.includes("personal workspace")) {
      response = await fetch(`${apiBaseUrl()}/v1.0/myorg/datasets/${encodeURIComponent(datasetId)}`, {
        headers: { Authorization: "Bearer " + token }
      });
    }
  }
''' + AUTH_CHECK
TOKEN_SPAWN_OLD = '{ encoding: "utf8", shell: true, windowsHide: true }'
TOKEN_SPAWN_NEW = '{ encoding: "utf8", shell: process.platform === "win32", windowsHide: true }'
SCREENSHOT_WINDOWS = '''1. Resolve the Windows local application-data folder with
   `[Environment]::GetFolderPath('LocalApplicationData')` and use
   `<LocalApplicationData>\\Power BI Report Authoring\\Screenshots` as the
   parent. Do not hard-code `C:\\Users\\<user>\\AppData\\Local`; the configured user
   profile may be on another local drive.'''
SCREENSHOT_MAC = '''1. On macOS, resolve the local application-data directory using Python:

   ```bash
   python3 -c 'from pathlib import Path; print(Path.home() / "Library/Application Support/Power BI Report Authoring/Screenshots")'
   ```

   Use that exact resolved absolute path as the default screenshot parent. On
   Windows, resolve `[Environment]::GetFolderPath('LocalApplicationData')` and
   append `Power BI Report Authoring\\Screenshots`. Do not hard-code a username.
   Preserve the approval, temporary-child, review, and cleanup rules below.'''


def run(command, **kwargs):
    return subprocess.run(command, check=True, **kwargs)


def node_major(version):
    match = re.fullmatch(r"v?(\d+)\.\d+\.\d+(?:[-+].*)?", version.strip())
    if not match:
        raise ValueError("Cannot parse Node.js version")
    return int(match.group(1))


def patch_report_cli(dist):
    """Fail before any write if either pinned entry point has unexpected code."""
    prepared = []
    for relative in ("cli.js", "preview/index.js"):
        path = dist / relative
        source = path.read_text()
        if PATCH_MARKER in source:
            if source.count(PERSONAL_WORKSPACE_PATCH) != 1:
                raise ValueError(f"Unrecognized existing patch in {relative}")
            continue
        if source.count(AUTH_CHECK) != 1:
            raise ValueError(f"Unexpected report CLI code in {relative}; no files changed")
        backup = path.with_name(path.name + ".before-my-workspace.bak")
        if backup.exists() and backup.read_text() != source:
            raise ValueError(f"Conflicting backup for {relative}; no files changed")
        prepared.append((path, backup, source.replace(AUTH_CHECK, PERSONAL_WORKSPACE_PATCH)))
    for path, backup, updated in prepared:
        if not backup.exists():
            shutil.copy2(path, backup)
        path.write_text(updated)


def model_server(binary, mode):
    if mode not in ("read-only", "read-write"):
        raise ValueError("Invalid model mode")
    args = ["--" + mode]
    if mode == "read-write":
        args.append("--require-confirmation")
    return {"type": "stdio", "command": str(binary), "args": args}


def patch_token_spawn(cli):
    source = cli.read_text()
    if TOKEN_SPAWN_NEW in source:
        return
    if source.count(TOKEN_SPAWN_OLD) != 1:
        raise ValueError("Unexpected token-spawn implementation; refusing to patch")
    backup = cli.with_name(cli.name + ".before-safe-token-spawn.bak")
    if not backup.exists():
        shutil.copy2(cli, backup)
    cli.write_text(source.replace(TOKEN_SPAWN_OLD, TOKEN_SPAWN_NEW))


def prepare_report_skill(checkout, destination):
    """Copy the skill's dependency layout, leaving the pinned Git checkout clean."""
    source = checkout / "skills/powerbi-report-cli"
    target = destination / "skills/powerbi-report-cli"
    if not destination.exists():
        destination.mkdir(parents=True)
        shutil.copytree(source, target)
        shutil.copytree(checkout / "common", destination / "common")
        shutil.copy2(checkout / "LICENSE", destination / "LICENSE")
    if not (target / "SKILL.md").is_file() or (target / "SKILL.md").read_bytes() != (source / "SKILL.md").read_bytes():
        raise ValueError("Existing copied report skill differs from the pinned version")
    lifecycle = target / "references/authoring/preview-part-03.md"
    text = lifecycle.read_text()
    if SCREENSHOT_MAC not in text:
        if text.count(SCREENSHOT_WINDOWS) != 1:
            raise ValueError("Unexpected screenshot lifecycle; refusing to patch")
        lifecycle.write_text(text.replace(SCREENSHOT_WINDOWS, SCREENSHOT_MAC))
    return target


def register_server(config, server, replace=False):
    """Preserve other servers; refuse JSONC and conflicts rather than destroying them."""
    if config.exists():
        try:
            data = json.loads(config.read_text())
        except json.JSONDecodeError as exc:
            raise ValueError("MCP config is JSONC or invalid JSON; use a separate --config file") from exc
    else:
        data = {}
    if not isinstance(data, dict):
        raise ValueError("MCP config must be a JSON object")
    servers = data.setdefault("servers", {})
    if not isinstance(servers, dict):
        raise ValueError("MCP servers must be a JSON object")
    if SERVER_NAME in servers and servers[SERVER_NAME] != server and not replace:
        raise ValueError("Existing powerbi-macos server differs; use --replace-existing explicitly")
    if servers.get(SERVER_NAME) == server:
        return
    if config.exists():
        digest = hashlib.sha256(config.read_bytes()).hexdigest()[:12]
        backup = config.with_name(config.name + f".before-powerbi-{digest}.bak")
        if not backup.exists():
            shutil.copy2(config, backup)
    servers[SERVER_NAME] = server
    config.parent.mkdir(parents=True, exist_ok=True)
    staged = config.with_name(config.name + ".powerbi-staged")
    staged.write_text(json.dumps(data, indent=2) + "\n")
    staged.chmod(0o600)
    staged.replace(config)


def register_skill(source, destination):
    if not (source / "SKILL.md").is_file():
        raise ValueError("Downloaded report skill is missing")
    if destination.exists() or destination.is_symlink():
        if destination.is_symlink() and destination.resolve() == source.resolve():
            return
        raise ValueError(f"Existing skill will not be overwritten: {destination}")
    destination.parent.mkdir(parents=True, exist_ok=True)
    destination.symlink_to(source, target_is_directory=True)


def write_wrapper(path, node, cli):
    # JSON encoding safely forms JavaScript strings even when paths contain spaces.
    source = '''#!/usr/bin/env node
// Use macOS system CAs without disabling TLS verification.
const {spawnSync} = require('node:child_process');
const result = spawnSync(NODE, ['--use-system-ca', CLI, ...process.argv.slice(2)], {stdio: 'inherit'});
if (result.error) {console.error(result.error.message); process.exit(1);}
if (result.signal) {console.error('Report CLI terminated by ' + result.signal); process.exit(1);}
process.exit(result.status ?? 1);
'''.replace("NODE,", json.dumps(str(node)) + ",").replace("CLI,", json.dumps(str(cli)) + ",")
    path.parent.mkdir(parents=True, exist_ok=True)
    if path.exists() and path.read_text() != source:
        raise ValueError(f"Refusing to replace an unrelated executable: {path}")
    path.write_text(source)
    path.chmod(0o755)


def preflight():
    if platform.system() != "Darwin" or platform.machine() != "arm64":
        raise ValueError("This workflow is tested only on native Apple Silicon macOS")
    for command in ("node", "npm", "git", "codesign"):
        if not shutil.which(command):
            raise ValueError(f"Install {command} before continuing")
    node = Path(shutil.which("node")).resolve()
    version = run([str(node), "--version"], capture_output=True, text=True).stdout
    if node_major(version) < 24:
        raise ValueError("Node.js 24 or later is required for the system CA option")
    return node


def install(args):
    node = preflight()
    prefix = args.prefix.expanduser().resolve()
    package_dir = prefix / "packages"
    dist = package_dir / "node_modules/@microsoft/powerbi-report-authoring-cli/dist"
    source_dist = package_dir / "node_modules/@microsoft/powerbi-modeling-mcp-darwin-arm64/dist"
    # npm does not run package lifecycle scripts. Chromium is installed explicitly below.
    run(["npm", "install", "--prefix", str(package_dir), "--ignore-scripts", "--save-exact",
         f"@microsoft/powerbi-modeling-mcp@{MODEL_VERSION}",
         f"@microsoft/powerbi-report-authoring-cli@{REPORT_VERSION}"])
    report_package = json.loads((dist.parent / "package.json").read_text())
    if report_package.get("version") != REPORT_VERSION:
        raise ValueError("Unexpected report CLI version; refusing to patch")
    patch_report_cli(dist)
    patch_token_spawn(dist / "cli.js")
    for relative in ("cli.js", "preview/index.js"):
        run([str(node), "--check", str(dist / relative)])

    destination = prefix / "model" / MODEL_VERSION
    binary = destination / "powerbi-modeling-mcp"
    if not binary.exists():
        # Retain resources and configuration next to the copied executable.
        shutil.copytree(source_dist, destination)
    signed = subprocess.run(["codesign", "--verify", str(binary)], capture_output=True)
    if signed.returncode:
        run(["codesign", "--sign", "-", str(binary)])
    run(["codesign", "--verify", str(binary)])
    run([str(binary), "--help"], capture_output=True, text=True)

    skills = prefix / "skills-for-fabric"
    if not skills.exists():
        run(["git", "clone", "--no-checkout", "https://github.com/microsoft/skills-for-fabric.git", str(skills)])
        run(["git", "-C", str(skills), "checkout", "--detach", SKILLS_COMMIT])
    actual = run(["git", "-C", str(skills), "rev-parse", "HEAD"], capture_output=True, text=True).stdout.strip()
    if actual != SKILLS_COMMIT:
        raise ValueError("Skill checkout differs from the tested commit; refusing to change it")
    if run(["git", "-C", str(skills), "status", "--porcelain"], capture_output=True, text=True).stdout.strip():
        raise ValueError("Skill checkout has local changes; refusing to register it")

    wrapper = prefix / "bin" / "powerbi-report-author"
    write_wrapper(wrapper, node, dist / "cli.js")
    if not args.skip_browser:
        run([str(node), "--use-system-ca", str(package_dir / "node_modules/playwright/cli.js"), "install", "chromium"])
    run([str(node), str(wrapper), "doctor"])
    smoke = Path(__file__).with_name("mcp-smoke-test.py")
    run([sys.executable, str(smoke), str(binary)])

    # Register only after the signed executable and CLI pass startup checks.
    report_skill = prepare_report_skill(skills, prefix / "report-skill-package")
    skill_destination = args.skill_directory.expanduser()
    # Migrate only a symlink made by an earlier run of this installer, not another installation.
    if skill_destination.is_symlink() and skill_destination.resolve() == (skills / "skills/powerbi-report-cli").resolve():
        skill_destination.unlink()
    register_skill(report_skill, skill_destination)
    register_server(args.config.expanduser(), model_server(binary, args.model_mode), args.replace_existing)
    manifest = {"modelVersion": MODEL_VERSION, "reportCliVersion": REPORT_VERSION,
                "skillsCommit": SKILLS_COMMIT, "modelMode": args.model_mode,
                "config": str(args.config.expanduser()), "reportCommand": str(wrapper),
                "personalWorkspacePatch": True, "macScreenshotLifecycle": True,
                "browserInstalled": not args.skip_browser}
    (prefix / "installation.json").write_text(json.dumps(manifest, indent=2) + "\n")
    print("Setup complete. Reload VS Code and enable powerbi-macos and the powerbi-report-cli skill.")
    print(f"Report CLI: {wrapper}")
    print("Review and accept the Microsoft EULA in your MCP client before model operations.")
    print("No reports or models have been uploaded or modified by this installer.")


def main():
    home = Path.home()
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--prefix", type=Path, default=home / "Library/Application Support/powerbi-macos")
    parser.add_argument("--config", type=Path, default=home / "Library/Application Support/Code/User/mcp.json")
    parser.add_argument("--skill-directory", type=Path, default=home / ".copilot/skills/powerbi-report-cli")
    parser.add_argument("--model-mode", choices=("read-only", "read-write"), default="read-only")
    parser.add_argument("--replace-existing", action="store_true", help="Replace this helper's conflicting MCP entry")
    parser.add_argument("--skip-browser", action="store_true", help="Skip Chromium download; preview may not be available")
    args = parser.parse_args()
    try:
        install(args)
    except (OSError, ValueError, subprocess.CalledProcessError) as exc:
        print(f"Setup failed: {exc}", file=sys.stderr)
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main())
