#!/usr/bin/env python3
"""Check a local MCP executable without accepting its EULA or opening a model."""

import argparse
from contextlib import suppress
import json
import os
import queue
import signal
import subprocess
import sys
import threading
import time
from pathlib import Path


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("binary", type=Path)
    parser.add_argument("--timeout", type=float, default=30)
    args = parser.parse_args()
    if args.timeout <= 0:
        parser.error("--timeout must be positive")

    process = subprocess.Popen(
        [str(args.binary.resolve()), "--start"],
        stdin=subprocess.PIPE, stdout=subprocess.PIPE, stderr=subprocess.PIPE,
        text=True, bufsize=1, start_new_session=(os.name == "posix"),
    )
    messages = queue.Queue()
    diagnostics = []

    def read_stdout():
        for line in process.stdout:
            messages.put(line)
        messages.put(None)

    def read_stderr():
        for line in process.stderr:
            diagnostics.append(line.rstrip())
            del diagnostics[:-30]

    readers = [threading.Thread(target=read_stdout, daemon=True),
               threading.Thread(target=read_stderr, daemon=True)]
    for reader in readers:
        reader.start()
    deadline = time.monotonic() + args.timeout

    def send(message):
        process.stdin.write(json.dumps(message) + "\n")
        process.stdin.flush()

    def response(request_id):
        while True:
            remaining = deadline - time.monotonic()
            if remaining <= 0:
                raise RuntimeError("Timed out waiting for MCP response")
            try:
                line = messages.get(timeout=remaining)
            except queue.Empty as exc:
                raise RuntimeError("Timed out waiting for MCP response") from exc
            if line is None:
                raise RuntimeError("Server closed stdout before responding")
            try:
                message = json.loads(line)
            except json.JSONDecodeError as exc:
                raise RuntimeError("Non-JSON output on MCP stdout") from exc
            if not isinstance(message, dict) or message.get("jsonrpc") != "2.0":
                raise RuntimeError("Invalid JSON-RPC output")
            if message.get("id") != request_id:
                continue
            if "error" in message:
                raise RuntimeError(f"MCP error: {message['error']}")
            if not isinstance(message.get("result"), dict):
                raise RuntimeError("Missing MCP result")
            return message["result"]

    try:
        send({"jsonrpc": "2.0", "id": 1, "method": "initialize", "params": {
            "protocolVersion": "2024-11-05", "capabilities": {},
            "clientInfo": {"name": "powerbi-macos-smoke-test", "version": "1.0"},
        }})
        initialization = response(1)
        if not initialization.get("serverInfo") or not initialization.get("protocolVersion"):
            raise RuntimeError("Incomplete initialize response")
        send({"jsonrpc": "2.0", "method": "notifications/initialized"})
        names = set()
        cursor = None
        cursors = set()
        request_id = 2
        while True:
            send({"jsonrpc": "2.0", "id": request_id, "method": "tools/list",
                  "params": {} if cursor is None else {"cursor": cursor}})
            result = response(request_id)
            tools = result.get("tools")
            if not isinstance(tools, list):
                raise RuntimeError("Missing tools list")
            for tool in tools:
                if not isinstance(tool, dict) or not isinstance(tool.get("name"), str):
                    raise RuntimeError("Invalid tool entry")
                names.add(tool["name"])
            cursor = result.get("nextCursor")
            if cursor is None:
                break
            if not isinstance(cursor, str) or cursor in cursors:
                raise RuntimeError("Invalid or repeated tools cursor")
            cursors.add(cursor)
            request_id += 1
        if "connection_operations" not in names or "model_operations" not in names:
            raise RuntimeError("Expected Power BI tools were not discovered")
        print(json.dumps({"status": "PASS", "server": initialization["serverInfo"],
                          "protocolVersion": initialization["protocolVersion"],
                          "toolCount": len(names), "tools": sorted(names)}, indent=2))
        return 0
    except (RuntimeError, BrokenPipeError) as exc:
        print(f"FAIL: {exc}", file=sys.stderr)
        for line in diagnostics:
            print(line, file=sys.stderr)
        return 1
    finally:
        if process.poll() is None:
            if os.name == "posix":
                with suppress(ProcessLookupError):
                    os.killpg(process.pid, signal.SIGTERM)
            else:
                process.terminate()
        try:
            process.wait(timeout=5)
        except subprocess.TimeoutExpired:
            if os.name == "posix":
                with suppress(ProcessLookupError):
                    os.killpg(process.pid, signal.SIGKILL)
            else:
                process.kill()
            process.wait()
        for reader in readers:
            reader.join(timeout=1)
        if process.returncode not in (0, -15, 143):
            print(f"Server exit code: {process.returncode}", file=sys.stderr)
        with suppress(BrokenPipeError):
            process.stdin.close()
        process.stdout.close()
        process.stderr.close()


if __name__ == "__main__":
    sys.exit(main())
