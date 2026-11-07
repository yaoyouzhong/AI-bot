"""Exercise IDE and device frames on ONE real CLI stream using a local model fixture.

This tests the protocol underneath the VS Code extension, not the extension UI
or a connection to an already running process. Never loads the user's Claude
profile. The first model response is held until the device append is written.
"""
import argparse
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
import json
import os
import hmac
import secrets
import socket
from pathlib import Path
import queue
import subprocess
import tempfile
import threading
import time
import uuid


class Model(BaseHTTPRequestHandler):
    def log_message(self, *args):
        pass

    def do_POST(self):
        request=json.loads(self.rfile.read(int(self.headers.get("Content-Length",0))))
        if self.path.split("?")[0]!="/v1/messages" or not request.get("messages"):
            self.send_error(404)
            return
        texts=[]
        for msg in request["messages"]:
            if msg.get("role")!="user": continue
            content=msg.get("content",[])
            texts.extend([content] if isinstance(content,str) else [b.get("text","") for b in content if isinstance(b,dict)])
        device=any("fixture-device-append" in part for part in texts)
        initial=any("fixture-ide-first" in part for part in texts)
        if device or initial:
            gate=self.server.second if device else self.server.first
            gate.set()
            (self.server.release_second if device else self.server.release_first).wait(35)
        body=json.dumps({"id":"msg_fixture_"+uuid.uuid4().hex,"type":"message","role":"assistant",
            "model":"claude-sonnet-4-6","content":[{"type":"text","text":"fixture reply"}],
            "stop_reason":"end_turn","stop_sequence":None,"usage":{"input_tokens":4,"output_tokens":4}}).encode()
        try:
            self.send_response(200);self.send_header("Content-Type","application/json")
            self.send_header("Content-Length",str(len(body)));self.end_headers();self.wfile.write(body)
        except (BrokenPipeError, ConnectionResetError): pass


def run(cli,wrapper=None):
    api=ThreadingHTTPServer(("127.0.0.1",0),Model)
    for name in ("first","second","release_first","release_second"):
        setattr(api,name,threading.Event())
    threading.Thread(target=api.serve_forever,daemon=True).start()
    marks={}
    try:
        with tempfile.TemporaryDirectory(prefix="aibot-api-fixture-") as folder:
            root=Path(folder)
            env={k:os.environ[k] for k in ("SystemRoot","WINDIR","COMSPEC","PATH") if k in os.environ}
            env.update({k:folder for k in ("HOME","USERPROFILE","APPDATA","LOCALAPPDATA","TMP","TEMP","TMPDIR","XDG_CONFIG_HOME")})
            env.update(CLAUDE_CONFIG_DIR=str(root/"config"),ANTHROPIC_API_KEY="fixture-key",
                       ANTHROPIC_BASE_URL=f"http://127.0.0.1:{api.server_port}",CLAUDE_CODE_DISABLE_NONESSENTIAL_TRAFFIC="1",
                       DISABLE_TELEMETRY="1",DISABLE_ERROR_REPORTING="1")
            sid=str(uuid.uuid4())
            args=[str(cli),"--bare","--print","--input-format","stream-json","--output-format","stream-json","--verbose",
                  "--replay-user-messages","--session-id="+sid,"--strict-mcp-config","--tools","","--disable-slash-commands",
                  "--no-session-persistence","--model","claude-sonnet-4-6"]
            peer=None
            if wrapper is not None:
                args.remove("--session-id="+sid)
                sid=""  # The plugin begins with no predetermined session ID.
                server=socket.socket()
                server.bind(("127.0.0.1",0));server.listen(1);server.settimeout(12)
                secret=secrets.token_bytes(32)
                env.update(AIBOT_PROBE_PORT=str(server.getsockname()[1]),AIBOT_PROBE_SECRET=secret.hex())
                args.insert(0,str(wrapper))
            with subprocess.Popen(args,cwd=root,env=env,stdin=subprocess.PIPE,stdout=subprocess.PIPE,stderr=subprocess.DEVNULL,
                                  creationflags=getattr(subprocess,"CREATE_NO_WINDOW",0)) as child:
                if wrapper is not None:
                    try:
                        sock,_=server.accept()
                        peer=sock.makefile("rwb",buffering=0)
                        challenge=bytes.fromhex(peer.readline(128).decode().strip())
                        if len(challenge)!=32:raise RuntimeError("invalid_probe_challenge")
                        peer.write(hmac.digest(secret,challenge,"sha256").hex().encode()+b"\n")
                        if peer.readline(64).strip()!=b"ready":raise RuntimeError("wrapper_unavailable")
                        marks["separateIdeAndDeviceStreams"]=True
                    finally:server.close()
                frames=queue.Queue(); seen=[]
                def read():
                    for line in child.stdout:
                        try: frames.put(json.loads(line))
                        except ValueError: frames.put({"type":"malformed"})
                    frames.put({"type":"eof"})
                threading.Thread(target=read,daemon=True).start()
                def write(obj,device=False):
                    out=peer if peer is not None and device else child.stdin
                    out.write(json.dumps(obj).encode()+b"\n");out.flush()
                def wait_for(test,timeout=25):
                    until=time.monotonic()+timeout
                    while True:
                        for frame in seen:
                            if test(frame):return frame
                        delay=until-time.monotonic()
                        if delay<=0:raise RuntimeError("frame_timeout")
                        try:frame=frames.get(timeout=delay)
                        except queue.Empty:raise RuntimeError("frame_timeout") from None
                        if frame["type"]=="eof":raise RuntimeError("unexpected_eof")
                        seen.append(frame)
                def user(text):
                    uid=str(uuid.uuid4())
                    write({"type":"user","session_id":sid,"uuid":uid,"parent_tool_use_id":None,
                        "message":{"role":"user","content":[{"type":"text","text":text}]}},device=(text=="fixture-device-append"))
                    return uid
                def control(subtype,**data):
                    rid=str(uuid.uuid4())
                    write({"type":"control_request","request_id":rid,"request":{"subtype":subtype,**data}},device=(subtype=="interrupt"))
                    if peer is not None and subtype=="interrupt":
                        private=json.loads(peer.readline(4096))
                        while "ended" in private:private=json.loads(peer.readline(4096))
                        if private.get("type")!="control_response" or private.get("response",{}).get("request_id")!=rid:raise RuntimeError("unexpected_private_receipt")
                        marks["privateInterruptReceipt"]=True
                        return private["response"]
                    return wait_for(lambda f:f.get("type")=="control_response" and f.get("response",{}).get("request_id")==rid)["response"]
                first=second=None
                try:
                    if control("initialize")["subtype"]!="success":raise RuntimeError("initialize_failed")
                    marks["initialize"]=True
                    first=user("fixture-ide-first")
                    if not api.first.wait(25):raise RuntimeError("first_model_call_missing")
                    if peer is not None:
                        sid=str(uuid.UUID(json.loads(peer.readline(512))["boundSession"]))
                        marks["sessionBoundFromCli"]=True
                    second=user("fixture-device-append")
                    marks["appendWrittenDuringFirst"]=not api.release_first.is_set()
                    try:
                        lifecycle=wait_for(lambda f:f.get("type")=="command_lifecycle" and second in f.values(),2)
                        marks["queueFrame"]={"command_uuid_matches":lifecycle.get("command_uuid")==second,"state":lifecycle.get("state"),"session_matches":lifecycle.get("session_id")==sid}
                    except RuntimeError as e:
                        if str(e)!="frame_timeout":raise
                    api.release_first.set()
                    if not api.second.wait(30):raise RuntimeError("queued_input_missing")
                    marks["appendReachedModel"]=True
                    wait_for(lambda f:f.get("type")=="result" and f.get("terminal_reason")=="completed" and
                             (f.get("user_message_uuid")==first or first in (f.get("user_message_uuids") or [])))
                    ack=control("interrupt",cancel_queued=True)
                    if ack["subtype"]!="success":raise RuntimeError("interrupt_declined")
                    marks["interruptAck"]=True
                    result=wait_for(lambda f:f.get("type")=="result" and f.get("session_id")==sid and
                                    (f.get("user_message_uuid")==second or second in (f.get("user_message_uuids") or [])) and
                                    (f.get("terminal_reason")=="interrupted" or f.get("terminal_reason")=="aborted_streaming" and f.get("is_error") is True))
                    marks["terminalReason"]=result["terminal_reason"]
                    marks["interruptedSameTurn"]=not api.release_second.is_set()
                    for uid in (first,second):
                        wait_for(lambda f:f.get("type")=="user" and f.get("uuid")==uid and f.get("session_id")==sid,3)
                    marks["bothUserReceiptsSameSession"]=True
                except Exception as e:
                    while not frames.empty():seen.append(frames.get_nowait())
                    marks["failure"]=str(e) if isinstance(e,RuntimeError) else type(e).__name__
                    marks["frameTypes"]=sorted({f.get("type","unknown") for f in seen})
                    marks["resultReasons"]=[f.get("terminal_reason") for f in seen if f.get("type")=="result"]
                    marks["resultSubtypes"]=[f.get("subtype") for f in seen if f.get("type")=="result"]
                    marks["receipts"]=[{"type":f["type"],"sameSession":f.get("session_id")==sid,"first":bool(first and first in (f.get("uuid"),f.get("user_message_uuid"))),"second":bool(second and second in (f.get("uuid"),f.get("user_message_uuid")))} for f in seen if f.get("type") in ("result","user")]
                finally:
                    api.release_first.set();api.release_second.set()
                    child.stdin.close()
                    if peer is not None:peer.close();sock.close()
                    try:child.wait(3)
                    except subprocess.TimeoutExpired:
                        child.kill();child.wait()
    finally:
        api.shutdown();api.server_close()
    return marks

if __name__=="__main__":
    parser=argparse.ArgumentParser();parser.add_argument("cli",type=Path);parser.add_argument("--wrapper",type=Path);args=parser.parse_args()
    result=run(args.cli.resolve(strict=True),args.wrapper.resolve(strict=True) if args.wrapper else None)
    print(json.dumps(result))
    raise SystemExit(0 if result.get("interruptedSameTurn") and result.get("bothUserReceiptsSameSession") and "failure" not in result else 1)
