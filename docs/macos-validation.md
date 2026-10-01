# Apple Silicon macOS validation

This report provides reproducible evidence for a macOS extension contribution.
It does not add supported macOS distribution or change the server implementation.

## Extension installation

On 2026-10-01, the public Marketplace gallery API listed version 1.0.0 of
`analysis-services.powerbi-modeling-mcp` for `win32-x64`, `win32-arm64`,
`linux-x64`, and `linux-arm64`. It did not list a `darwin-arm64`, `darwin-x64`,
or universal package for that version.

VS Code selects platform-specific packages for the running platform. Publishing
a compatible macOS package is therefore a prerequisite for normal installation.
See [platform-specific extension packaging](https://code.visualstudio.com/api/working-with-extensions/publishing-extension#platform-specific-extensions).

## Server startup

The locally installed `@microsoft/powerbi-modeling-mcp-darwin-arm64@1.0.0`
executable was unsigned: `codesign -dv <binary>` reported no signature. Direct
execution of `--help` terminated with SIGKILL (Python subprocess return code -9).

For diagnosis, a separate copy of the complete distribution directory was made
and its executable was ad-hoc signed with `codesign --sign - <copy>`. The copy
then ran `--help` successfully. Ad-hoc signing is a local experiment, not a
production signing or notarization solution.

Run the included test against a supplied executable:

```sh
python3 scripts/mcp-smoke-test.py /absolute/path/to/powerbi-modeling-mcp
```

The test holds stdin open, initializes MCP, sends the initialized notification,
and discovers tools. It rejects non-JSON protocol output, handles pagination,
and uses a bounded timeout. It does not accept the EULA, authenticate, invoke a
model operation, or connect to a semantic model.

The signed 1.0.0 copy passed initialization with protocol `2024-11-05` and
returned 22 tools, including `connection_operations` and `model_operations`.
The handshake ran outside the Codex execution sandbox; startup stalled inside
that sandbox. No Fabric connectivity or PBIP/TMDL authoring claim follows from
this protocol test.

## Proposed implementation and release work

- Package and publish `darwin-arm64` with the Mac executable and resources,
  preserving executable permissions and selecting the name without `.exe`.
- Sign and notarize the Mac payload using the publisher's release identity,
  then validate the installed VSIX on a clean Mac under normal Gatekeeper policy.
- Validate extension activation and MCP registration in VS Code, followed by
  PBIP/TMDL authoring and Fabric authentication, XMLA connectivity, and DAX queries.
- Explain or disable operations that require a local Windows Desktop instance.
- Fix signal handling in the npm platform launcher: the inspected 1.0.0
  `resolve(code || 0)` reports a signal-killed child as success. Handle the signal
  explicitly and report failure on stderr.
- Add Mac installation and protocol tests to the release pipeline. Validate
  Intel Mac support separately before publishing a `darwin-x64` target.

The public repository at commit `2b25defb975169ffb573195cc0ff8006835b8a6e`
contains documentation and issue templates, but no extension, server, launcher,
or release pipeline source. Maintainers need to provide the source location or
apply the implementation changes in their source repository.

Coordinate through the existing [Apple Silicon request](https://github.com/microsoft/powerbi-modeling-mcp/issues/16)
and [startup bug report](https://github.com/microsoft/powerbi-modeling-mcp/issues/87).
