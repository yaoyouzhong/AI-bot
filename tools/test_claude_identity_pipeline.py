"""Real Claude CLI -> candidate loopback server, using synthetic API and HOME."""
import argparse
import json
import os
from pathlib import Path
import shutil
import signal
import socket
import subprocess
import sys
import tempfile
import threading
import time
from http.server import HTTPServer
import urllib.request
import uuid

from probe_claude_terminal_events import SyntheticAPI


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--bridge", required=True)
    args = parser.parse_args()
    executable = shutil.which("claude")
    if not executable: raise SystemExit("Claude CLI unavailable")
    runner = Path(__file__).with_name("claude_identity_runner.py").resolve()
    release = threading.Event()

    class API(SyntheticAPI):
        def do_POST(self):
            if self.server.mode == "success": return super().do_POST()
            self.rfile.read(int(self.headers.get("Content-Length", "0")))
            if self.server.mode == "interrupt": release.wait(15)
            body = b'{"type":"error","error":{"type":"invalid_request_error","message":"synthetic failure"}}'
            try:
                self.send_response(400); self.send_header("Content-Type", "application/json")
                self.send_header("Content-Length", str(len(body))); self.end_headers(); self.wfile.write(body)
            except (BrokenPipeError, ConnectionResetError): pass

    api = HTTPServer(("127.0.0.1", 0), API); api.mode = "success"
    threading.Thread(target=api.serve_forever, daemon=True).start()
    with socket.socket() as probe:
        probe.bind(("127.0.0.1", 0)); port = probe.getsockname()[1]
    bridge = subprocess.Popen([str(Path(args.bridge).resolve()), "--self-test-claude-events-server", str(port)], stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
    opener = urllib.request.build_opener(urllib.request.ProxyHandler({}))
    def status():
        request = urllib.request.Request(f"http://127.0.0.1:{port}/status", headers={"X-AIBot-Token":"t" * 40})
        with opener.open(request, timeout=1) as response: return json.load(response)
    try:
        for _ in range(100):
            try: status(); break
            except OSError: time.sleep(0.05)
        else: raise AssertionError("candidate server unavailable")
        sid, pid = str(uuid.uuid4()), str(uuid.uuid4())
        for name, expected in [("UserPromptSubmit","running"),("StopFailure","failed")]:
            raw = {"hook_event_name":name,"session_id":sid,"prompt_id":pid,"prompt":"private synthetic text","cwd":"private synthetic path"}
            forwarded = subprocess.run([str(Path(args.bridge).resolve()),"--claude-hook",str(port)],input=json.dumps(raw).encode(),capture_output=True,timeout=5)
            assert not forwarded.stdout and forwarded.returncode == 0
            assert status()["outcome"] == expected, "native hook forwarding"
        print("NATIVE_HOOK_PIPELINE_OK", flush=True)
        for mode, outcome in [("success", "completed"), ("failure", "failed"), ("interrupt", "interrupted")]:
            api.mode = mode
            with tempfile.TemporaryDirectory(prefix="aibot-identity-pipeline-") as directory:
                Path(directory, ".claude.json").write_text(json.dumps({"hasCompletedOnboarding":True}), encoding="utf-8")
                env = {"PATH":"/usr/bin:/bin:/usr/sbin:/sbin", "HOME":directory, "TMPDIR":directory,
                    "ANTHROPIC_BASE_URL":f"http://127.0.0.1:{api.server_port}", "ANTHROPIC_API_KEY":"synthetic-key",
                    "CLAUDE_CODE_DISABLE_NONESSENTIAL_TRAFFIC":"1", "DISABLE_TELEMETRY":"1", "DISABLE_ERROR_REPORTING":"1"}
                command = [sys.executable, str(runner), "run", "--port", str(port), "--claude", executable, "--",
                    "--model", "claude-sonnet-4-6", "--no-session-persistence", "--setting-sources", "user",
                    "--strict-mcp-config", "--tools", "", "--disable-slash-commands", "--max-budget-usd", "0.05"]
                task = subprocess.Popen(command, stdin=subprocess.PIPE, stdout=subprocess.PIPE, stderr=subprocess.PIPE, cwd=directory, env=env)
                try:
                    if mode == "interrupt":
                        task.stdin.write(b"Reply only OK."); task.stdin.close(); task.stdin = None
                        for _ in range(100):
                            if status()["state"] == "working": break
                            time.sleep(0.05)
                        else: raise AssertionError("start identity missing before interrupt")
                        task.send_signal(signal.SIGINT)
                        output, errors = task.communicate(timeout=15)
                        release.set()
                    else: output, errors = task.communicate(b"Reply only OK.", timeout=30)
                    observed = status()
                    assert observed["outcome"] == outcome, (mode, observed)
                    assert observed["completionActive"] == (mode == "success"), (mode, observed)
                    print(mode.upper() + "_PIPELINE_OK", flush=True)
                finally:
                    if task.poll() is None: task.kill(); task.wait()
        print("CLAUDE_IDENTITY_PIPELINE_OK no real credentials, conversation or device", flush=True)
    finally:
        release.set(); bridge.terminate(); bridge.wait(timeout=5); api.shutdown(); api.server_close()


if __name__ == "__main__": main()
