"""Opt-in Windows VS Code UI trial. Uses an isolated profile, the installed
vendor extension and a local synthetic model. Only --open launches a window.
"""
import argparse
import hmac
import json
import os
from pathlib import Path
import secrets
import socket
import subprocess
import sys
import tempfile
import threading
import time
import uuid
from http.server import ThreadingHTTPServer

from probe_claude_vscode_session_api import Model


def trial(code, extension, wrapper, output):
    output.mkdir(exist_ok=False)
    def report(stage, **data):
        path=output/"report.tmp"
        path.write_text(json.dumps({"stage":stage,**data},indent=2),encoding="utf-8")
        path.replace(output/"report.json")
    report("preparing")
    api=ThreadingHTTPServer(("127.0.0.1",0),Model)
    for name in ("first","second","release_first","release_second"):
        setattr(api,name,threading.Event())
    threading.Thread(target=api.serve_forever,daemon=True).start()
    try:
        with socket.socket() as server:
            server.bind(("127.0.0.1",0));server.listen(1);server.settimeout(240)
            secret=secrets.token_bytes(32)
            root=Path(tempfile.mkdtemp(prefix="aibot-vscode-api-"))
            profile=root/"profile";workspace=root/"fixture-workspace";extensions=root/"extensions"
            for path in [workspace,extensions,profile/"User",root/"home",root/"claude"] :path.mkdir(parents=True)
            # This isolated profile supplies its own local synthetic provider.
            settings={"claudeCode.claudeProcessWrapper":str(wrapper),"claudeCode.disableLoginPrompt":True,"telemetry.telemetryLevel":"off",
                      "extensions.autoUpdate":False,"extensions.autoCheckUpdates":False,"update.mode":"none",
                      "workbench.startupEditor":"none"}
            (profile/"User/settings.json").write_text(json.dumps(settings),encoding="utf-8")
            (workspace/"TEST.txt").write_text("Open Claude Code and send fixture-ide-first once. Local model fixture.",encoding="utf-8")
            (root/"claude/.claude.json").write_text('{"hasCompletedOnboarding":true}',encoding="utf-8")
            env={k:os.environ[k] for k in ("PATH","SystemRoot","WINDIR","COMSPEC","TEMP","TMP") if k in os.environ}
            env.update(HOME=str(root/"home"),USERPROFILE=str(root/"home"),APPDATA=str(root/"home"),LOCALAPPDATA=str(root/"home"),
                       CLAUDE_CONFIG_DIR=str(root/"claude"),ANTHROPIC_API_KEY="fixture-key",
                       ANTHROPIC_BASE_URL=f"http://127.0.0.1:{api.server_port}",CLAUDE_CODE_DISABLE_NONESSENTIAL_TRAFFIC="1",
                       DISABLE_TELEMETRY="1",DISABLE_ERROR_REPORTING="1",AIBOT_PROBE_PORT=str(server.getsockname()[1]),
                       AIBOT_PROBE_SECRET=secret.hex())
            report("waiting_for_test_panel",isolatedProfile=str(profile))
            subprocess.Popen([str(code),"--new-window","--user-data-dir",str(profile),"--extensions-dir",str(extensions),
                              "--extensionDevelopmentPath="+str(extension),"--disable-updates","--skip-welcome",str(workspace)],env=env,cwd=workspace)
            sock,_=server.accept()
            with sock:
                sock.settimeout(180)
                with sock.makefile("rwb",buffering=0) as peer:
                    challenge=bytes.fromhex(peer.readline(128).decode().strip())
                    if len(challenge)!=32:raise ValueError("invalid_challenge")
                    peer.write(hmac.digest(secret,challenge,"sha256").hex().encode()+b"\n")
                    if peer.readline(64).strip()!=b"ready":raise ValueError("wrapper_auth_failed")
                    report("waiting_for_fixture_input")
                    if not api.first.wait(100):raise ValueError("fixture_model_request_not_observed")
                    sid=str(uuid.UUID(json.loads(peer.readline(512))["boundSession"]))
                    uid=str(uuid.uuid4());rid=str(uuid.uuid4())
                    def send(frame):peer.write(json.dumps(frame).encode()+b"\n")
                    send({"type":"user","session_id":sid,"uuid":uid,"parent_tool_use_id":None,
                          "message":{"role":"user","content":[{"type":"text","text":"fixture-device-append"}]}})
                    api.release_first.set()
                    if not api.second.wait(25):raise ValueError("append_not_delivered")
                    send({"type":"control_request","request_id":rid,"request":{"subtype":"interrupt","cancel_queued":True}})
                    ack=ended=False;deadline=time.monotonic()+30
                    while not (ack and ended):
                        sock.settimeout(max(.01,deadline-time.monotonic()))
                        frame=json.loads(peer.readline(4096))
                        reply=frame.get("response",{})
                        ack|=frame.get("type")=="control_response" and reply.get("request_id")==rid and reply.get("subtype")=="success"
                        ended|=frame.get("ended")==uid and frame.get("session")==sid and frame.get("reason") in ("interrupted","aborted_streaming")
                    report("passed",appendReachedModel=True,interruptAck=True,sameSessionTerminalResult=True,uiReadback="pending")
    except (ValueError,OSError,KeyError,TypeError) as exc:
        report("not_passed",reason=str(exc) if type(exc) is ValueError and str(exc).isidentifier() else type(exc).__name__)
        return 1
    finally:
        api.release_first.set();api.release_second.set();api.shutdown();api.server_close()
    return 0


if __name__=="__main__":
    p=argparse.ArgumentParser();p.add_argument("--code",type=Path,required=True);p.add_argument("--extension",type=Path,required=True)
    p.add_argument("--wrapper",type=Path,required=True);p.add_argument("--output",type=Path,required=True);p.add_argument("--open",action="store_true")
    a=p.parse_args();code=a.code.resolve(strict=True);wrapper=a.wrapper.resolve(strict=True);ext=a.extension.resolve(strict=True)
    manifest=json.loads((ext/"package.json").read_text(encoding="utf-8"))
    if manifest.get("publisher","").lower()!="anthropic" or manifest.get("name")!="claude-code":sys.exit("expected_installed_claude_extension")
    if not a.open:print("VS_CODE_ISOLATED_TRIAL_READY");sys.exit(0)
    sys.exit(trial(code,ext,wrapper,a.output.resolve()))
