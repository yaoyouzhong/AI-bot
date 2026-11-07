"""Run the terminal relay and workspace against native Claude with a loopback model.

Build the Swift tests first. The child processes use a disposable Claude
profile and an outbound network sandbox. No terminal window is opened.
"""
import fcntl
import io
import json
import os
from pathlib import Path
import pty
import shutil
import signal
import struct
import subprocess
import sys
import termios
import threading
import time
import uuid
from http.server import ThreadingHTTPServer

from probe_mac_claude_background import Model


class InteractiveModel(Model):
    def do_POST(self):
        body = self.rfile.read(int(self.headers.get("Content-Length", 0)))
        request = json.loads(body)
        messages = request.get("messages", [])
        latest = messages[-1].get("content", "") if messages else ""
        text = latest if isinstance(latest, str) else " ".join(
            block.get("text", "") for block in latest if isinstance(block, dict)
        )
        for kind in ("append", "interrupt"):
            if f"fixture running for {kind}" in text:
                (self.server.fixture / f"{kind}-busy").touch()
                until = time.monotonic() + 12
                while not (self.server.fixture / f"{kind}-release").is_file() and time.monotonic() < until:
                    time.sleep(0.05)
        stream = self.rfile
        self.rfile = io.BytesIO(body)
        try:
            super().do_POST()
        except (BrokenPipeError, ConnectionResetError):
            pass
        finally:
            self.rfile = stream


def probe(root, api, terminal_program):
    cli = shutil.which("claude")
    if sys.platform != "darwin" or not cli:
        raise RuntimeError("macOS and native Claude Code 2.1.318 are required")
    profile = '(version 1)(allow default)(deny network-outbound (remote ip "*:*"))(allow network-outbound (remote ip "localhost:*"))'
    cfg, cwd = (root / p for p in ("cfg", "project"))
    for directory in (cfg, cwd):
        directory.mkdir(mode=0o700)
    (cfg / ".claude.json").write_text(json.dumps({"hasCompletedOnboarding": True,
        "projects": {str(cwd): {"hasTrustDialogAccepted": True, "hasCompletedProjectOnboarding": True}}}))
    env = {"PATH": "/usr/bin:/bin:/usr/sbin:/sbin", "TERM": "xterm-256color", "LANG":"en_US.UTF-8",
           "CLAUDE_CONFIG_DIR": str(cfg), "TERM_PROGRAM": terminal_program,
           "ANTHROPIC_BASE_URL": f"http://127.0.0.1:{api.server_port}",
           "CLAUDE_CODE_DISABLE_NONESSENTIAL_TRAFFIC": "1", "DISABLE_TELEMETRY": "1", "DISABLE_ERROR_REPORTING": "1"}
    helper = root / "key"
    helper.write_text("#!/bin/sh\nprintf fixture-local-key\n")
    helper.chmod(0o700)
    settings = cfg / "settings.json"
    settings.write_text(json.dumps({"apiKeyHelper": str(helper), "env": {k: v for k,v in env.items() if k.startswith(("ANTHROPIC_","DISABLE_","CLAUDE_CODE_"))}}))
    flags = ["--model", "claude-sonnet-4-6", "--tools", "", "--strict-mcp-config", "--settings", str(settings)]
    session = str(uuid.uuid4())
    sandbox = ["/usr/bin/sandbox-exec", "-p", profile, cli]
    seed = subprocess.run(sandbox + ["--print", "--output-format", "json", "--session-id", session, *flags, "fixture seed"],
                          cwd=cwd, env=env, capture_output=True, timeout=20)
    assert seed.returncode == 0 and api.calls > 0, "mock seed"
    api.reply = "fixture direct answer"
    app = str((Path.cwd() / "mac-app/.build/debug/AIBotBridge").resolve())
    cmd = ["/usr/bin/sandbox-exec", "-p", profile, app, "--claude-terminal", "--resume", session]
    pid, fd = pty.fork()
    if pid == 0:
        os.chdir(cwd)
        fcntl.ioctl(0, termios.TIOCSWINSZ, struct.pack("HHHH", 35, 120, 0, 0))
        os.execve(cmd[0], cmd, env)

    def drain():
        try:
            while os.read(fd, 65536):
                pass
        except OSError:
            pass
    threading.Thread(target=drain, daemon=True).start()
    def rows():
        return [json.loads(p.read_bytes()) for p in (cfg / "sessions").glob("*.json")]
    try:
        for _ in range(100):
            worker = next((r for r in rows() if r.get("sessionId") == session and r.get("status") == "idle"), None)
            if worker:
                break
            time.sleep(.1)
        else:
            raise RuntimeError("native interactive Claude did not become idle")
        time.sleep(0.5)
        os.write(fd, b"fixture desktop draft")
        time.sleep(0.35)
        (root / "draft-ready").touch()

        def clear_draft():
            until = time.monotonic() + 10
            while not (root / "draft-checked").is_file() and time.monotonic() < until:
                time.sleep(0.05)
            os.write(fd, bytes([21]))
            time.sleep(0.4)
            (root / "draft-cleared").touch()

        threading.Thread(target=clear_draft, daemon=True).start()
        test_env = dict(os.environ, AIBOT_TERMINAL_FIXTURE=str(cfg), AIBOT_TERMINAL_SESSION=session, AIBOT_TERMINAL_BINARY=app)
        subprocess.run(["swift", "test", "--skip-build", "--package-path", "mac-app", "--filter",
                        "MacClaudeTerminalIntegrationTests.testInteractiveTerminalFixture"],
                       env=test_env, check=True, timeout=90)
        if api.photo_calls != [(True, ["image/jpeg", "image/png", "image/jpeg"])]:
            raise RuntimeError("the local model did not receive the original images in order")
        print("MAC_CLAUDE_TERMINAL_OK", terminal_program,
              "interactive session, text/photos, queued append, exact interrupt")
    finally:
        os.close(fd)
        try:
            os.kill(pid, signal.SIGTERM)
        except ProcessLookupError:
            pass
        try:
            os.waitpid(pid, 0)
        except ChildProcessError:
            pass


if __name__ == "__main__":
    import tempfile
    os.chdir(Path(__file__).resolve().parents[1])
    for terminal_program in ("Apple_Terminal", "iTerm.app"):
        with ThreadingHTTPServer(("127.0.0.1", 0), InteractiveModel) as api:
            api.calls, api.reply, api.photo_calls = 0, "fixture seed response", []
            threading.Thread(target=api.serve_forever, daemon=True).start()
            with tempfile.TemporaryDirectory(prefix="aict-", dir="/private/tmp") as directory:
                api.fixture = Path(directory)
                try:
                    probe(api.fixture, api, terminal_program)
                finally:
                    api.shutdown()
