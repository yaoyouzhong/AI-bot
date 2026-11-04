"""Real Claude stream/control protocol with temporary HOME and loopback fixtures.

Never uses real credentials, projects, prompts or conversations. Reports fixed
pass/fail labels and field names only; does not persist raw protocol frames.
"""
import json
import queue
import shutil
import subprocess
import tempfile
import threading
import time
import uuid
from pathlib import Path
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer

class API(BaseHTTPRequestHandler):
    def log_message(self, *args): pass
    def do_POST(self):
        self.rfile.read(int(self.headers.get('Content-Length','0')))
        self.server.calls += 1
        self.server.entered.set()
        if self.server.slow: self.server.release.wait(15)
        body=json.dumps({'id':'msg_fixture_'+str(self.server.calls),'type':'message','role':'assistant','model':'claude-sonnet-4-6','content':[{'type':'text','text':'Synthetic public reply'}],'stop_reason':'end_turn','stop_sequence':None,'usage':{'input_tokens':5,'output_tokens':2}}).encode()
        try:
            self.send_response(200);self.send_header('Content-Type','application/json');self.send_header('Content-Length',str(len(body)));self.end_headers();self.wfile.write(body)
        except (BrokenPipeError,ConnectionResetError): pass

def main():
    cli=shutil.which('claude')
    if not cli:raise SystemExit('Claude CLI unavailable')
    api=ThreadingHTTPServer(('127.0.0.1',0),API);api.calls=0;api.slow=False;api.entered=threading.Event();api.release=threading.Event()
    threading.Thread(target=api.serve_forever,daemon=True).start()
    try:
        with tempfile.TemporaryDirectory(prefix='aibot-claude-stream-') as directory:
            root=Path(directory);(root/'.claude.json').write_text(json.dumps({'hasCompletedOnboarding':True}))
            env={'PATH':'/usr/bin:/bin:/usr/sbin:/sbin','HOME':directory,'TMPDIR':directory,'ANTHROPIC_BASE_URL':f'http://127.0.0.1:{api.server_port}','ANTHROPIC_API_KEY':'synthetic-key','CLAUDE_CODE_DISABLE_NONESSENTIAL_TRAFFIC':'1','DISABLE_TELEMETRY':'1','DISABLE_ERROR_REPORTING':'1'}
            sid=str(uuid.uuid4())
            command=[cli,'--print','--input-format','stream-json','--output-format','stream-json','--verbose','--replay-user-messages','--permission-prompt-tool','stdio','--session-id='+sid,'--model','claude-sonnet-4-6','--setting-sources','user','--strict-mcp-config','--tools','','--disable-slash-commands','--max-budget-usd','0.05']
            process=subprocess.Popen(command,stdin=subprocess.PIPE,stdout=subprocess.PIPE,stderr=subprocess.DEVNULL,text=True,env=env,cwd=directory)
            frames=queue.Queue(); observed=[]; current={}; write_lock=threading.Lock()
            def read():
                for line in process.stdout:
                    try:
                        frame=json.loads(line);observed.append(frame.get('type','unknown'));
                        if frame.get('type')=='control_request' and frame.get('request',{}).get('subtype')=='hook_callback':
                            payload=frame['request'].get('input',{})
                            print('STREAM_PROMPT_HOOK_ID_MATCH',payload.get('prompt_id')==current.get('message'),payload.get('session_id')==sid,'fields='+','.join(sorted(payload)),flush=True)
                            send({'type':'control_response','response':{'subtype':'success','request_id':frame['request_id'],'response':{}}})
                        frames.put(frame)
                    except json.JSONDecodeError:frames.put({'type':'non_json'})
                frames.put({'type':'eof'})
            threading.Thread(target=read,daemon=True).start()
            def send(value):
                with write_lock:process.stdin.write(json.dumps(value)+'\n');process.stdin.flush()
            def wait(predicate,timeout=20):
                until=time.monotonic()+timeout
                while time.monotonic()<until:
                    frame=frames.get(timeout=max(.01,until-time.monotonic()))
                    if frame.get('type')=='eof':raise AssertionError('CLI exited before expected frame')
                    if predicate(frame):return frame
                raise AssertionError('expected frame missing')
            def prompt():
                message=str(uuid.uuid4());current['message']=message;send({'type':'user','session_id':sid,'uuid':message,'parent_tool_use_id':None,'message':{'role':'user','content':[{'type':'text','text':'Synthetic test only; no tools.'}]}});return message
            try:
                request=str(uuid.uuid4());send({'type':'control_request','request_id':request,'request':{'subtype':'initialize','hooks':{'UserPromptSubmit':[{'hookCallbackIds':['observe-prompt']}]}}})
                init=wait(lambda v:v.get('type')=='control_response' and v.get('response',{}).get('request_id')==request)
                assert init['response']['subtype']=='success';print('STREAM_INITIALIZE_OK',flush=True)
                for index in range(2):
                    message=prompt()
                    replay=wait(lambda v:v.get('type')=='user' and v.get('uuid')==message)
                    result=wait(lambda v:v.get('type')=='result')
                    assert result.get('is_error') is False and result.get('terminal_reason')=='completed' and result.get('session_id')==sid
                    print('STREAM_SAME_SESSION_TURN_OK',index+1,'result_fields='+','.join(sorted(result)),flush=True)
                api.slow=True;api.entered.clear();message=prompt()
                assert api.entered.wait(5)
                request=str(uuid.uuid4());send({'type':'control_request','request_id':request,'request':{'subtype':'interrupt','cancel_queued':True}})
                ack=wait(lambda v:v.get('type')=='control_response' and v.get('response',{}).get('request_id')==request)
                assert ack['response']['subtype']=='success'
                result=wait(lambda v:v.get('type')=='result')
                print('STREAM_INTERRUPT_RESULT_FIELDS',','.join(sorted(result)),flush=True)
                assert result.get('terminal_reason')!='completed' or result.get('is_error') is True
                print('STREAM_INTERRUPT_OK receipt_fields='+','.join(sorted(ack['response'].get('response',{}))),flush=True)
                print('STREAM_FRAME_TYPES',','.join(observed),flush=True)
                print('CLAUDE_STREAM_CONTROL_PROBE_OK synthetic loopback only',flush=True)
            finally:
                api.release.set()
                if process.poll() is None:process.terminate()
                try:process.wait(timeout=5)
                except subprocess.TimeoutExpired:process.kill();process.wait()
    finally:api.release.set();api.shutdown();api.server_close()
if __name__=='__main__':main()
