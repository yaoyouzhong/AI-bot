"""Opt-in Claude task runner for candidate lifecycle verification (Python 3).

Does not edit user settings. Only identity and lifecycle metadata go to the
loopback bridge. Existing independent interactive sessions use --claude-hook;
this runner can certify final results only for the process it starts itself.
"""
import argparse
import json
import os
from pathlib import Path
import subprocess
import sys
import tempfile
import time
import urllib.request
import uuid

EVENTS = ("UserPromptSubmit", "PreToolUse", "PostToolUse", "PostToolUseFailure", "PostToolBatch",
          "PermissionRequest", "Elicitation", "Stop", "StopFailure", "Notification", "SessionEnd")


def identity(raw):
    try:
        return {"sessionId": str(uuid.UUID(raw["session_id"])), "promptId": str(uuid.UUID(raw["prompt_id"]))}
    except (ValueError, KeyError, TypeError, AttributeError):
        return None


def post(port, event):
    payload = dict(event, agent="claude", emittedAt=int(time.time() * 1000))
    request = urllib.request.Request(f"http://127.0.0.1:{port}/event", data=json.dumps(payload).encode(),
                                     headers={"Content-Type": "application/json"}, method="POST")
    try:
        # Loopback must not pass through a configured network proxy.
        with urllib.request.build_opener(urllib.request.ProxyHandler({})).open(request, timeout=1) as response:
            return response.status == 200
    except OSError:
        return False


def hook(args):
    try:
        raw = json.loads(sys.stdin.buffer.read(8_388_609))
        bound = identity(raw)
        kind = raw.get("hook_event_name")
        if not bound or "agent_id" in raw or kind not in EVENTS:
            return
        if kind == "UserPromptSubmit":
            # Ephemeral local correlation only, never a transcript or title.
            Path(args.identity).write_text(json.dumps(bound), encoding="utf-8")
        event = dict(bound, event=kind)
        if kind == "Notification":
            if raw.get("notification_type") != "idle_prompt":
                return
            event["notificationType"] = "idle_prompt"
        post(args.port, event)
    except (ValueError, OSError, TypeError):
        pass  # A diagnostic hook must never alter permission decisions.


def run(args, extra):
    # Extra options are CLI options explicitly supplied by the caller. The
    # runner owns framing and settings so a task cannot silently lose identity.
    forbidden = {"-p", "--print", "--output-format", "--input-format", "--settings"}
    if any(x.split("=", 1)[0] in forbidden for x in extra):
        raise SystemExit("Runner owns print, format and temporary settings options")
    with tempfile.TemporaryDirectory(prefix="aibot-claude-run-") as directory:
        bound_file = Path(directory) / "identity.json"
        command_parts = [sys.executable, str(Path(__file__).resolve()), "hook", "--port", str(args.port), "--identity", str(bound_file)]
        settings = Path(directory) / "settings.json"
        settings.write_text(json.dumps({"hooks": {name: [{"hooks": [{"type": "command", "command": command_parts[0], "args": command_parts[1:]}]}] for name in EVENTS}}), encoding="utf-8")
        task = subprocess.Popen([args.claude, "--print", "--output-format", "json", "--settings", str(settings), *extra],
                                stdin=None, stdout=subprocess.PIPE, stderr=None)
        interrupted = False
        try:
            output, _ = task.communicate()
        except KeyboardInterrupt:
            interrupted = True
            task.terminate()
            try: output, _ = task.communicate(timeout=5)
            except subprocess.TimeoutExpired:
                task.kill(); output, _ = task.communicate()
        try: bound = json.loads(bound_file.read_text(encoding="utf-8"))
        except (OSError, ValueError): bound = None
        try: result = json.loads(output)
        except (ValueError, TypeError): result = {}
        if not isinstance(result, dict): result = {}
        if bound:
            if interrupted or task.returncode < 0:
                post(args.port, dict(bound, event="TurnInterrupted"))
            elif result.get("session_id") == bound["sessionId"] and result.get("type") == "result":
                reason = result.get("terminal_reason")
                post(args.port, dict(bound, event="TurnResult", terminalReason=reason or "unconfirmed",
                                     isError=result.get("is_error") is not False or task.returncode != 0,
                                     numTurns=result.get("num_turns", 0)))
        # Public CLI output stays with the invoking terminal, never the device.
        if output: sys.stdout.buffer.write(output)
        return 130 if interrupted else task.returncode


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    sub = parser.add_subparsers(dest="mode", required=True)
    hook_parser = sub.add_parser("hook")
    hook_parser.add_argument("--port", type=int, required=True)
    hook_parser.add_argument("--identity", required=True)
    run_parser = sub.add_parser("run")
    run_parser.add_argument("--port", type=int, default=8765)
    run_parser.add_argument("--claude", default="claude")
    args, extra = parser.parse_known_args()
    if not 0 < args.port <= 65535: parser.error("invalid port")
    if extra[:1] == ["--"]: extra = extra[1:]
    if args.mode == "hook": hook(args); return 0
    return run(args, extra)


if __name__ == "__main__": sys.exit(main())
