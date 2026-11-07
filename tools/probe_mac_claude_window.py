"""Prepare an ordinary native Claude window backed only by a loopback model.

Run on macOS, then execute the printed .command in Terminal. This is
the native Claude executable, without a relay or bridge launcher. The optional
--activate-terminal flag explicitly permits opening a disposable Terminal
window. Test requests have no tools, real credentials or external networking.
Keep this process running while executing the printed XCTest command.
"""
import argparse
from http.server import ThreadingHTTPServer
import io
import json
import os
from pathlib import Path
import platform
import shlex
import shutil
import subprocess
import tempfile
import threading
import time
import uuid

from probe_mac_claude_background import Model


class WindowModel(Model):
    def do_POST(self):
        original = self.rfile
        body = original.read(int(self.headers.get("Content-Length", 0)))
        request = json.loads(body)
        messages = request.get("messages", [])
        content = messages[-1].get("content", "") if messages else ""
        text = content if isinstance(content, str) else " ".join(
            block.get("text", "") for block in content if isinstance(block, dict))
        for kind in ("append", "interrupt"):
            if "fixture running for " + kind in text:
                (self.server.fixture / (kind + "-busy")).touch()
                deadline = time.monotonic() + 120
                while not (self.server.fixture / (kind + "-release")).exists():
                    if time.monotonic() >= deadline:
                        break
                    time.sleep(0.05)
        self.rfile = io.BytesIO(body)
        try:
            super().do_POST()
        except (BrokenPipeError, ConnectionResetError):
            pass
        finally:
            self.rfile = original
            (self.server.fixture / "audit.json").write_text(json.dumps({
                "calls": self.server.calls, "photos": self.server.photo_calls,
            }))


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--activate-terminal", action="store_true")
    args = parser.parse_args()
    if platform.system() != "Darwin":
        parser.error("Requires macOS")
    cli = shutil.which("claude")
    if not cli:
        parser.error("Install native Claude Code first")
    root = Path(tempfile.mkdtemp(prefix="aibot-window-")).resolve()
    config, project = root / ".claude", root / "Fixture"
    config.mkdir(mode=0o700)
    project.mkdir(mode=0o700)
    api = ThreadingHTTPServer(("127.0.0.1", 0), WindowModel)
    api.calls, api.photo_calls, api.reply, api.fixture = 0, [], "fixture window reply", root
    threading.Thread(target=api.serve_forever, daemon=True).start()
    profile = {"hasCompletedOnboarding": True, "projects": {str(project): {
        "hasTrustDialogAccepted": True, "hasCompletedProjectOnboarding": True}}}
    for base in (root, config):
        (base / ".claude.json").write_text(json.dumps(profile))
    helper = root / "local-key"
    helper.write_text("#!/bin/sh\nprintf synthetic-key\n")
    helper.chmod(0o700)
    provider = {
        "ANTHROPIC_BASE_URL": f"http://127.0.0.1:{api.server_port}",
        "CLAUDE_CODE_DISABLE_NONESSENTIAL_TRAFFIC": "1",
        "DISABLE_TELEMETRY": "1", "DISABLE_ERROR_REPORTING": "1",
    }
    settings = config / "settings.json"
    settings.write_text(json.dumps({"apiKeyHelper": str(helper), "env": provider}))
    env = dict(provider, HOME=str(root), TMPDIR=str(root), CLAUDE_CONFIG_DIR=str(config),
               PATH="/usr/bin:/bin:/usr/sbin:/sbin", LANG="en_US.UTF-8", TERM="xterm-256color")
    sandbox = ('(version 1)(allow default)(deny network-outbound (remote ip "*:*"))'
               '(allow network-outbound (remote ip "localhost:*"))')
    base = ["/usr/bin/sandbox-exec", "-p", sandbox, cli]
    flags = ["--model", "claude-sonnet-4-6", "--tools", "", "--strict-mcp-config", "--settings", str(settings)]
    session = str(uuid.uuid4())
    seed = subprocess.run(base + ["--print", "--session-id", session, "--name", "AI-bot Window Test", *flags, "fixture seed"],
                          env=env, cwd=project, capture_output=True, timeout=30)
    if seed.returncode or api.calls != 1:
        raise RuntimeError("Local model fixture failed; no external request allowed")
    (root / "terminal-session").write_text(session)
    (root / ".aibot-synthetic-claude").write_text("window\n")
    (root / "environment.json").write_text(json.dumps(dict(
        provider, HOME=str(root), TMPDIR=str(root), PATH=env["PATH"], ANTHROPIC_API_KEY="synthetic-key")))
    command = root / "Start.command"
    command.write_text("#!/bin/zsh\ncd " + shlex.quote(str(project)) + "\nexec /usr/bin/env -i " +
                       " ".join(shlex.quote(k + "=" + v) for k, v in env.items()) + " " +
                       shlex.join(base + ["--resume", session, *flags]) + "\n")
    command.chmod(0o700)
    print(json.dumps({"fixture": str(root), "nativeCommand": str(command)}, ensure_ascii=False), flush=True)
    print("AIBOT_WINDOW_FIXTURE=" + shlex.quote(str(root)) +
          " swift test --package-path mac-app --filter MacClaudeTerminalWindowTests.testExistingWindowFixture", flush=True)
    if args.activate_terminal:
        subprocess.run(["open", "-a", "Terminal", str(command)], check=True)
    try:
        while True:
            time.sleep(1)
    except KeyboardInterrupt:
        pass
    finally:
        api.shutdown()
        api.server_close()
        # Retain only this disposable profile for inspection and window exit.
        # It contains synthetic requests and a dummy key, never user credentials.


if __name__ == "__main__":
    main()
