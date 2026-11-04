"""Probe local Claude hook ordering against a synthetic loopback API.

Uses a temporary home, dummy credential, and fixed test data. Never records
request headers, request bodies, hook payloads, or actual conversation text.
"""
import json
import os
from pathlib import Path
import shutil
import subprocess
import tempfile
import threading
from http.server import BaseHTTPRequestHandler, HTTPServer


class SyntheticAPI(BaseHTTPRequestHandler):
    def log_message(self, *args):
        pass

    def do_POST(self):
        self.rfile.read(int(self.headers.get("Content-Length", "0")))
        body = json.dumps({"id": "msg_synthetic", "type": "message", "role": "assistant",
                           "model": "claude-sonnet-4-6", "content": [{"type": "text", "text": "OK"}],
                           "stop_reason": "end_turn", "stop_sequence": None,
                           "usage": {"input_tokens": 5, "output_tokens": 2}}).encode()
        self.send_response(200)
        self.send_header("Content-Type", "application/json")
        self.send_header("Content-Length", str(len(body)))
        self.end_headers()
        self.wfile.write(body)


def run_case(executable, block_first):
    server = HTTPServer(("127.0.0.1", 0), SyntheticAPI)
    threading.Thread(target=server.serve_forever, daemon=True).start()
    try:
        with tempfile.TemporaryDirectory(prefix="aibot-claude-event-probe-") as directory:
            root = Path(directory)
            (root / ".claude.json").write_text(json.dumps({"hasCompletedOnboarding": True}))
            events, marker, hook = root / "events.jsonl", root / "blocked", root / "hook.py"
            hook.write_text(
                "import json,os,sys\n"
                "a=json.load(sys.stdin)\n"
                "r={k:a.get(k) for k in ('hook_event_name','session_id','prompt_id','stop_hook_active')}\n"
                f"with open({str(events)!r},'a') as f: f.write(json.dumps(r)+'\\n')\n"
                f"marker={str(marker)!r}\n"
                f"if {block_first!r} and r['hook_event_name']=='Stop' and not os.path.exists(marker):\n"
                " open(marker,'w').close()\n"
                " print(json.dumps({'decision':'block','reason':'synthetic continuation'}))\n"
            )
            names = ["SessionStart", "UserPromptSubmit", "Stop", "StopFailure", "SessionEnd"]
            settings = root / "settings.json"
            settings.write_text(json.dumps({"hooks": {name: [{"hooks": [{"type": "command", "command": "/usr/bin/python3 " + str(hook)}]}] for name in names}}))
            # Do not inherit real credentials or user configuration.
            env = {"PATH": "/usr/bin:/bin:/usr/sbin:/sbin", "HOME": directory, "TMPDIR": directory,
                   "ANTHROPIC_BASE_URL": f"http://127.0.0.1:{server.server_port}",
                   "ANTHROPIC_API_KEY": "synthetic-key", "CLAUDE_CODE_DISABLE_NONESSENTIAL_TRAFFIC": "1",
                   "DISABLE_TELEMETRY": "1", "DISABLE_ERROR_REPORTING": "1"}
            command = [executable, "--print", "--output-format", "json", "--model", "claude-sonnet-4-6",
                       "--no-session-persistence", "--setting-sources", "user", "--settings", str(settings),
                       "--strict-mcp-config", "--tools", "", "--disable-slash-commands",
                       "--max-budget-usd", "0.05", "Reply only OK."]
            result = subprocess.run(command, env=env, cwd=directory, capture_output=True, text=True, timeout=30)
            outcome = json.loads(result.stdout)
            rows = [json.loads(line) for line in events.read_text().splitlines()]
            prompt = next(row for row in rows if row["hook_event_name"] == "UserPromptSubmit")
            stops = [row for row in rows if row["hook_event_name"] == "Stop"]
            assert result.returncode == 0 and outcome.get("is_error") is False
            assert outcome.get("terminal_reason") == "completed" and outcome.get("num_turns", 0) > 0
            assert len(stops) == (2 if block_first else 1)
            assert prompt["session_id"] and prompt["prompt_id"]
            assert all(row["session_id"] == prompt["session_id"] and row["prompt_id"] == prompt["prompt_id"] for row in stops)
            assert stops[0]["stop_hook_active"] is False
            if block_first:
                assert stops[1]["stop_hook_active"] is True
            print(f"{'BLOCKED_THEN_CONTINUED' if block_first else 'NORMAL_SUCCESS'}: {len(stops)} Stop event(s), result completed")
    finally:
        server.shutdown()
        server.server_close()


if __name__ == "__main__":
    executable = shutil.which("claude")
    if not executable:
        raise SystemExit("Claude CLI is unavailable; no probe was run")
    run_case(executable, False)
    run_case(executable, True)
    print("CLAUDE_TERMINAL_EVENT_PROBE_OK synthetic loopback API; no real credentials or conversations")
