"""Probe Claude interactive idle notifications using only synthetic local data.

Runs with a temporary HOME and local mock API. Hook output is restricted to
event type and identity equality; failed startup may print synthetic terminal data.
"""
import json
import os
import pty
import select
import shutil
import subprocess
import fcntl
import struct
import sys
import termios
import tempfile
import threading
import time
from pathlib import Path
from http.server import HTTPServer

from probe_claude_terminal_events import SyntheticAPI


def main():
    fail_after_stop = "--block-then-failure" in sys.argv[1:]
    block_first = "--block-first" in sys.argv[1:] or fail_after_stop
    fail_api = "--api-failure" in sys.argv[1:] or fail_after_stop
    marker_ref = []
    executable = shutil.which("claude")
    if not executable:
        raise SystemExit("Claude CLI unavailable")
    class FailureAPI(SyntheticAPI):
        def do_POST(self):
            if fail_after_stop and (not marker_ref or not marker_ref[0].exists()):
                return super().do_POST()
            self.rfile.read(int(self.headers.get("Content-Length", "0")))
            body = b'{"type":"error","error":{"type":"invalid_request_error","message":"synthetic failure"}}'
            self.send_response(400); self.send_header("Content-Type", "application/json")
            self.send_header("Content-Length", str(len(body))); self.end_headers(); self.wfile.write(body)
    server = HTTPServer(("127.0.0.1", 0), FailureAPI if fail_api else SyntheticAPI)
    threading.Thread(target=server.serve_forever, daemon=True).start()
    try:
        with tempfile.TemporaryDirectory(prefix="aibot-claude-idle-") as directory:
            root = Path(directory).resolve()
            (root / ".claude.json").write_text(json.dumps({
                "hasCompletedOnboarding": True,
                "projects": {str(root): {"hasTrustDialogAccepted": True}},
                "customApiKeyResponses": {"approved": ["synthetic-key"], "rejected": []},
            }))
            events = root / "events.jsonl"
            marker = root / "blocked"
            marker_ref.append(marker)
            hook = root / "hook.py"
            hook.write_text(
                "import json,sys\n"
                "a=json.load(sys.stdin)\n"
                "r={k:a.get(k) for k in ('hook_event_name','session_id','prompt_id','notification_type')}\n"
                f"with open({str(events)!r},'a') as f: f.write(json.dumps(r)+'\\n')\n"
                f"marker={str(marker)!r}\n"
                f"if {block_first!r} and r['hook_event_name']=='Stop' and not __import__('os').path.exists(marker):\n"
                " open(marker,'w').close()\n"
                " print(json.dumps({'decision':'block','reason':'synthetic continuation'}))\n"
            )
            settings = root / "settings.json"
            settings.write_text(json.dumps({"hooks": {name: [{"hooks": [{"type":"command", "command":"/usr/bin/python3 " + str(hook)}]}]
                                                       for name in ("UserPromptSubmit", "Stop", "StopFailure", "Notification")}}))
            env = {"PATH":"/usr/bin:/bin:/usr/sbin:/sbin", "HOME":directory, "TMPDIR":directory,
                   "TERM":"xterm-256color", "ANTHROPIC_BASE_URL":f"http://127.0.0.1:{server.server_port}",
                   "ANTHROPIC_API_KEY":"synthetic-key", "CLAUDE_CODE_DISABLE_NONESSENTIAL_TRAFFIC":"1",
                   "DISABLE_TELEMETRY":"1", "DISABLE_ERROR_REPORTING":"1"}
            master, slave = pty.openpty()
            fcntl.ioctl(slave, termios.TIOCSWINSZ, struct.pack("HHHH", 30, 100, 0, 0))
            process = subprocess.Popen([executable, "--model", "claude-sonnet-4-6",
                                        "--setting-sources", "user", "--settings", str(settings), "--tools", "",
                                        "--disable-slash-commands"], stdin=slave, stdout=slave, stderr=slave,
                                       cwd=directory, env=env, start_new_session=True)
            os.close(slave)
            rows = []
            terminal = bytearray()
            try:
                started = time.monotonic()
                submitted = False
                while time.monotonic() - started < 90:
                    if not submitted and time.monotonic() - started > 6:
                        os.write(master, b"Reply only OK.\r"); submitted = True
                    ready, _, _ = select.select([master], [], [], 0.2)
                    if ready:
                        try: terminal.extend(os.read(master, 65536)); terminal = terminal[-12000:]
                        except OSError: break
                    if events.exists():
                        try: rows = [json.loads(x) for x in events.read_text().splitlines()]
                        except (json.JSONDecodeError, OSError): pass
                    if time.monotonic() - started > 20 and not any(r.get("hook_event_name") == "UserPromptSubmit" for r in rows): break
                    if any(row.get("notification_type") == "idle_prompt" for row in rows): break
                    if process.poll() is not None: break
                prompt = next((r for r in rows if r.get("hook_event_name") == "UserPromptSubmit"), None)
                stop = next((r for r in rows if r.get("hook_event_name") == "Stop"), None)
                idle = next((r for r in rows if r.get("notification_type") == "idle_prompt"), None)
                stops = [r for r in rows if r.get("hook_event_name") == "Stop"]
                failures = [r for r in rows if r.get("hook_event_name") == "StopFailure"]
                print("interactive prompt:", bool(prompt), "stop count:", len(stops), "failure count:", len(failures), "idle notification:", bool(idle))
                if prompt and idle:
                    print("idle same session:", bool(prompt.get("session_id")) and prompt.get("session_id") == idle.get("session_id"),
                          "idle has prompt id:", bool(idle.get("prompt_id")),
                          "idle same prompt:", bool(prompt.get("prompt_id")) and prompt.get("prompt_id") == idle.get("prompt_id"))
                expected = bool(failures) and len(stops) == (1 if fail_after_stop else 0) if fail_api else bool(stop) and len(stops) == (2 if block_first else 1)
                if not (prompt and idle and expected):
                    print("synthetic-only terminal diagnostic:", terminal[-2000:].decode("utf-8", errors="replace"))
                    raise SystemExit("INCONCLUSIVE: interactive probe did not observe all events")
            finally:
                process.terminate()
                try: process.wait(timeout=3)
                except subprocess.TimeoutExpired: process.kill(); process.wait()
                os.close(master)
    finally:
        server.shutdown(); server.server_close()


if __name__ == "__main__": main()
