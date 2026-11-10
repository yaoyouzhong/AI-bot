"""Exercise the Mac bridge with an isolated Claude daemon and loopback model.

Run swift test --package-path mac-app first to build the XCTest fixture.
The probe uses a fresh CLAUDE_CONFIG_DIR, no real credentials, and a network
sandbox. Only the supervisor in the temporary config directory is stopped.
"""
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
import base64
import hashlib
import json
import os
from pathlib import Path
import platform
import shutil
import subprocess
import tempfile
import threading
import time
import uuid


class Model(BaseHTTPRequestHandler):
    def log_message(self, *_args):
        pass

    def do_POST(self):
        request = json.loads(self.rfile.read(int(self.headers.get("Content-Length", 0))))
        if self.path.split("?")[0] != "/v1/messages":
            self.send_error(404)
            return
        self.server.calls += 1
        reply = self.server.reply
        messages = request.get("messages", [])
        content = messages[-1].get("content", []) if messages else []
        images = [item["source"] for item in content if isinstance(item, dict) and item.get("type") == "image"]
        if images:
            index = len(self.server.photo_calls) + 1
            received = [hashlib.sha256(base64.b64decode(image["data"], validate=True)).hexdigest() for image in images]
            expected = self.server.fixture / f"expect-{index}.json"
            self.server.photo_calls.append((expected.is_file() and json.loads(expected.read_bytes()) == received,
                                            [image.get("media_type") for image in images]))
            release = self.server.fixture / f"release-{index}"
            deadline = time.monotonic() + 8
            while not release.is_file() and time.monotonic() < deadline:
                time.sleep(0.05)
            if not release.is_file():
                self.send_error(503)
                return
            reply = f"fixture photo response {index}"
        message = {
            "id": "msg_" + uuid.uuid4().hex, "type": "message", "role": "assistant",
            "model": "claude-sonnet-4-6", "content": [{"type": "text", "text": reply}],
            "stop_reason": "end_turn", "stop_sequence": None, "usage": {"input_tokens": 5, "output_tokens": 3},
        }
        if request.get("stream"):
            events = [
                ("message_start", {"message": dict(message, content=[], stop_reason=None)}),
                ("content_block_start", {"index": 0, "content_block": {"type": "text", "text": ""}}),
                ("content_block_delta", {"index": 0, "delta": {"type": "text_delta", "text": reply}}),
                ("content_block_stop", {"index": 0}),
                ("message_delta", {"delta": {"stop_reason": "end_turn", "stop_sequence": None}, "usage": {"output_tokens": 3}}),
                ("message_stop", {}),
            ]
            body = "".join(f"event: {kind}\ndata: {json.dumps(dict(payload, type=kind))}\n\n" for kind, payload in events).encode()
            content_type = "text/event-stream"
        else:
            body = json.dumps(message).encode()
            content_type = "application/json"
        self.send_response(200)
        self.send_header("Content-Type", content_type)
        self.send_header("Content-Length", str(len(body)))
        self.end_headers()
        self.wfile.write(body)


def run_fixture(cli, api, root):
    api.fixture, api.photo_calls = root, []
    config, cwd, scratch = (root / name for name in ("cfg", "project", "tmp"))
    for directory in (config, cwd, scratch):
        directory.mkdir(mode=0o700)
    profile = {"hasCompletedOnboarding": True, "projects": {
        str(cwd): {"hasTrustDialogAccepted": True, "hasCompletedProjectOnboarding": True}
    }}
    (config / ".claude.json").write_text(json.dumps(profile))
    provider = {
        "ANTHROPIC_BASE_URL": f"http://127.0.0.1:{api.server_port}",
        "ANTHROPIC_API_KEY": "fixture-local-key", "CLAUDE_CODE_DISABLE_NONESSENTIAL_TRAFFIC": "1",
        "DISABLE_TELEMETRY": "1", "DISABLE_ERROR_REPORTING": "1",
    }
    env = dict(provider, PATH="/usr/bin:/bin:/usr/sbin:/sbin", TERM="xterm-256color",
               TMPDIR=str(scratch), CLAUDE_CONFIG_DIR=str(config))
    key_helper = root / "fixture-key"
    key_helper.write_text("#!/bin/sh\nprintf 'fixture-local-key'\n")
    key_helper.chmod(0o700)
    settings = config / "settings.json"
    settings.write_text(json.dumps({"apiKeyHelper": str(key_helper), "env": provider}))
    sandbox = ('(version 1)(allow default)(deny network-outbound (remote ip "*:*"))'
               '(allow network-outbound (remote ip "localhost:*"))')

    def call(*args):
        return subprocess.run(["/usr/bin/sandbox-exec", "-p", sandbox, cli, *args],
                              cwd=cwd, env=env, capture_output=True, text=True, timeout=30)

    version = call("--version")
    if version.returncode != 0 or not version.stdout.startswith("2.1.318 "):
        raise RuntimeError("This probe requires native Claude Code 2.1.318")
    session = str(uuid.uuid4())
    flags = ["--model", "claude-sonnet-4-6", "--tools", "", "--strict-mcp-config", "--settings", str(settings)]
    seed = call("--print", "--output-format", "json", "--session-id", session, *flags, "fixture seed")
    if seed.returncode != 0 or api.calls == 0 or not list(config.rglob(session + ".jsonl")):
        raise RuntimeError("Could not seed isolated Claude history")
    api.reply = "fixture response"
    try:
        if call("--bg", "--resume", session, *flags).returncode != 0:
            raise RuntimeError("Could not resume the isolated session in the background")
        for _ in range(80):
            rows = [json.loads(path.read_bytes()) for path in (config / "sessions").glob("*.json")]
            if any(row.get("sessionId") == session and row.get("kind") == "bg" and row.get("status") == "idle"
                   for row in rows):
                break
            time.sleep(0.2)
        else:
            raise RuntimeError("The same session UUID did not become idle in the isolated daemon")
        package = Path(__file__).resolve().parents[1] / "mac-app"
        test_env = dict(os.environ, AIBOT_CLAUDE_BG_FIXTURE=str(config), AIBOT_CLAUDE_BG_SESSION=session)
        subprocess.run(["swift", "test", "--skip-build", "--package-path", str(package), "--filter",
                        "MacClaudeNativeTests.testBackgroundDaemonFixture"], env=test_env, check=True, timeout=90)
        expected_calls = [(True, ["image/jpeg", "image/png", "image/png"]), (True, ["image/jpeg"])]
        if api.photo_calls != expected_calls:
            raise RuntimeError("The loopback model did not receive the exact images in order")
    finally:
        lock = config / "daemon.lock"
        if lock.is_file() and json.loads(lock.read_bytes()).get("jsonPath") == str(config / "daemon.json"):
            if call("daemon", "stop", "--any").returncode != 0:
                raise RuntimeError("Could not stop the isolated Claude supervisor")


def main():
    cli = shutil.which("claude")
    if platform.system() != "Darwin" or not cli:
        raise SystemExit("Run on macOS with native Claude Code on PATH")
    with ThreadingHTTPServer(("127.0.0.1", 0), Model) as api:
        api.calls, api.reply = 0, "fixture seed response"
        threading.Thread(target=api.serve_forever, daemon=True).start()
        try:
            with tempfile.TemporaryDirectory(prefix="aibg-", dir="/private/tmp") as folder:
                run_fixture(cli, api, Path(folder))
            print("MAC_CLAUDE_BACKGROUND_OK same UUID, exact PNG/JPEG inputs, idempotent receipts, completed TAB5 replies")
        finally:
            api.shutdown()


if __name__ == "__main__":
    main()
