#!/usr/bin/env python3
"""Call Fabric with an Azure CLI token and macOS curl's verified TLS transport.

Tokens are passed through stdin, never command arguments, files, or stdout.
This is a transport helper; use the report skill for backup, packing, polling,
and verification. Requests that change remote state require --allow-write.
"""

import argparse
import json
import subprocess
import sys
import tempfile
from pathlib import Path
from urllib.parse import urlsplit


def validate_url(url):
    parsed = urlsplit(url)
    if (parsed.scheme != "https" or parsed.netloc != "api.fabric.microsoft.com"
            or not parsed.path.startswith("/v1/") or parsed.fragment
            or any(character in url for character in ("\n", "\r", "\x00"))):
        raise ValueError("Only https://api.fabric.microsoft.com/v1/ URLs are allowed")
    return url


def requires_write(method, url):
    path = urlsplit(url).path.rstrip("/")
    return method != "GET" and not (method == "POST" and path.endswith("/getDefinition"))


def curl_configuration(url, method, token, body=None):
    validate_url(url)
    if method not in ("GET", "POST", "PATCH"):
        raise ValueError("Unsupported request method")
    if not token or any(character in token for character in ("\n", "\r", "\x00")):
        raise ValueError("Invalid access token")
    settings = [f"url = {json.dumps(url)}", f"request = {json.dumps(method)}",
                f"header = {json.dumps('Authorization: Bearer ' + token)}",
                'header = "x-ms-fabric-skill: powerbi-report-cli"']
    if body is not None:
        settings.extend(['header = "Content-Type: application/json"',
                         f"data-binary = {json.dumps('@' + str(body.resolve()))}"])
    return "\n".join(settings) + "\n"


def parse_headers(raw):
    # Ignore proxy CONNECT and informational response blocks; use the final block.
    block = raw.replace("\r\n", "\n").strip().split("\n\n")[-1]
    headers = {}
    for line in block.splitlines()[1:]:
        if ":" in line:
            key, value = line.split(":", 1)
            if key.lower() in ("location", "retry-after", "x-ms-operation-id", "requestid"):
                headers[key.lower()] = value.strip()
    if "location" in headers:
        validate_url(headers["location"])
    return headers


def request(url, method="GET", body=None, allow_write=False):
    validate_url(url)
    if requires_write(method, url) and not allow_write:
        raise ValueError("Remote writes require explicit --allow-write")
    if body is not None:
        if not body.is_file():
            raise ValueError("Request body file does not exist")
        json.loads(body.read_text())
    token_result = subprocess.run(
        ["az", "account", "get-access-token", "--resource", "https://api.fabric.microsoft.com", "-o", "json"],
        capture_output=True, text=True, timeout=45, check=True,
    )
    token = json.loads(token_result.stdout)["accessToken"]
    with tempfile.TemporaryDirectory(prefix="powerbi-fabric-response-") as directory:
        headers_file = Path(directory) / "headers"
        body_file = Path(directory) / "body"
        # No -L or --insecure: do not forward credentials on redirects or bypass TLS.
        result = subprocess.run(
            ["curl", "--silent", "--show-error", "--max-time", "45", "--config", "-",
             "--dump-header", str(headers_file), "--output", str(body_file), "--write-out", "%{http_code}"],
            input=curl_configuration(url, method, token, body),
            capture_output=True, text=True, timeout=50, check=True,
        )
        status = int(result.stdout)
        headers = parse_headers(headers_file.read_text())
        raw = body_file.read_text()
        payload = json.loads(raw) if raw else None
        return {"status": status, "headers": headers, "body": payload}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("url")
    parser.add_argument("--method", choices=("GET", "POST", "PATCH"), default="GET")
    parser.add_argument("--body", type=Path)
    parser.add_argument("--allow-write", action="store_true")
    parser.add_argument("--out", type=Path, help="Save full response envelope, including LRO headers")
    parser.add_argument("--body-out", type=Path, help="Save body alone for unpack --input")
    args = parser.parse_args()
    try:
        for path in (args.out, args.body_out):
            if path is not None and path.exists():
                raise ValueError(f"Refusing to overwrite response artifact: {path}")
        if args.out is not None and args.body_out is not None and args.out.resolve() == args.body_out.resolve():
            raise ValueError("Response envelope and body require different output paths")
        envelope = request(args.url, args.method, args.body, args.allow_write)
        for path, value in ((args.out, envelope), (args.body_out, envelope["body"])):
            if path is not None:
                if path.exists():
                    raise ValueError(f"Refusing to overwrite response artifact: {path}")
                path.parent.mkdir(parents=True, exist_ok=True)
                with path.open("x") as output:
                    json.dump(value, output, indent=2)
                    output.write("\n")
                path.chmod(0o600)
        print(json.dumps({"status": envelope["status"], "headers": envelope["headers"],
                          "body": envelope["body"] if envelope["status"] >= 400 else "saved" if args.body_out else envelope["body"]}, indent=2))
        return 0 if 200 <= envelope["status"] < 300 else 1
    except (OSError, ValueError, subprocess.SubprocessError, KeyError) as exc:
        print(f"Fabric request failed: {exc}", file=sys.stderr)
        return 1


if __name__ == "__main__":
    sys.exit(main())
