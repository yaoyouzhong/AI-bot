"""Headless real-TAB5 acceptance with synthetic CLI sessions and loopback API.
No real project, credential or conversation is loaded. JSONL commands are explicit;
normal bridge must exit first. The temporary session directory is always removed.
"""
import json,os,shutil,subprocess,sys,tempfile,threading,time,uuid,urllib.request
from pathlib import Path
from http.server import BaseHTTPRequestHandler,ThreadingHTTPServer
class API(BaseHTTPRequestHandler):
    def log_message(self,*args):pass
    def do_POST(self):
        data=json.loads(self.rfile.read(int(self.headers.get('Content-Length','0'))));self.server.calls+=1
        latest=next((r.get('content','') for r in reversed(data.get('messages',[])) if r.get('role')=='user'),'')
        if 'slow' in str(latest).lower():self.server.release.wait(120)
        body=json.dumps({'id':'msg_tab5_'+str(self.server.calls),'type':'message','role':'assistant','model':'claude-sonnet-4-6','content':[{'type':'text','text':'\n\n'.join(f'第 {n} 段：TAB5 自动验收公开回复。只使用合成会话，检查滚动、翻页及客户端隔离。' for n in range(1,65))}],'stop_reason':'end_turn','stop_sequence':None,'usage':{'input_tokens':5,'output_tokens':2}}).encode()
        try:self.send_response(200);self.send_header('Content-Type','application/json');self.send_header('Content-Length',str(len(body)));self.end_headers();self.wfile.write(body)
        except (BrokenPipeError,ConnectionResetError):pass

def main():
    if len(sys.argv)!=3:raise SystemExit('Usage: run_tab5_workspace_acceptance.py APP_EXECUTABLE NEW_OUTPUT_DIRECTORY')
    executable=Path(sys.argv[1]).resolve();output=Path(sys.argv[2]).resolve();assert executable.is_file() and not output.exists()
    cli=shutil.which('claude');assert cli
    api=ThreadingHTTPServer(('127.0.0.1',0),API);api.calls=0;api.release=threading.Event();threading.Thread(target=api.serve_forever,daemon=True).start()
    process=None
    try:
        with tempfile.TemporaryDirectory(prefix='aibot-tab5-workspace-') as directory:
            root=Path(directory).resolve();(root/'.claude.json').write_text(json.dumps({'hasCompletedOnboarding':True}));(root/'.aibot-synthetic-claude').write_text('1\n')
            env={'PATH':'/usr/bin:/bin:/usr/sbin:/sbin','HOME':str(root),'TMPDIR':str(root),'ANTHROPIC_BASE_URL':f'http://127.0.0.1:{api.server_port}','ANTHROPIC_API_KEY':'synthetic-key','CLAUDE_CODE_DISABLE_NONESSENTIAL_TRAFFIC':'1','DISABLE_TELEMETRY':'1','DISABLE_ERROR_REPORTING':'1'}
            args=['--model','claude-sonnet-4-6','--setting-sources','user','--strict-mcp-config','--tools','','--disable-slash-commands','--max-budget-usd','0.05']
            sessions={}
            for label,folder in [('A','项目甲'),('B','项目甲'),('C','项目乙')]:
                cwd=root/folder;cwd.mkdir(exist_ok=True);sid=str(uuid.uuid4());sessions[label]=sid
                result=subprocess.run([cli,'--print','--output-format','json','--session-id='+sid,'--name','自动验收 Claude 会话 '+label,*args,'Synthetic seed; no tools.'],cwd=cwd,env=env,stdout=subprocess.PIPE,stderr=subprocess.DEVNULL,timeout=30)
                assert result.returncode==0 and json.loads(result.stdout).get('is_error') is False
            (root/'environment.json').write_text(json.dumps(env));
            process=subprocess.Popen([str(executable),'--tab5-automation',str(output),'--claude-fixture',str(root)],stdin=subprocess.PIPE,stdout=subprocess.PIPE,stderr=subprocess.STDOUT,text=True)
            def read():
                for line in process.stdout:print(line.rstrip(),flush=True)
            reader=threading.Thread(target=read,daemon=True);reader.start()
            print(json.dumps({'status':'synthetic-sessions-ready','sessions':sorted(sessions),'realCredentials':False}),flush=True)
            for line in sys.stdin:
                value=json.loads(line);op=value.get('op')
                if op=='select-overview':
                    sid=sessions[value['session']];body={'agent':'claude','event':'UserPromptSubmit','sessionId':sid,'promptId':str(uuid.uuid4()),'emittedAt':time.time()*1000}
                    request=urllib.request.Request('http://127.0.0.1:8765/event',data=json.dumps(body).encode(),headers={'Content-Type':'application/json'})
                    with urllib.request.urlopen(request,timeout=5) as response:assert response.status==200
                    print(json.dumps({'status':'overview-selected','session':value['session']}),flush=True)
                elif op=='release-api':api.release.set();print('{"status":"api-released"}',flush=True)
                elif op=='status':print(json.dumps({'status':'fixture-status','apiCalls':api.calls,'bridgeRunning':process.poll() is None}),flush=True)
                else:
                    if process.poll() is not None:raise RuntimeError('Automation bridge exited')
                    process.stdin.write(json.dumps(value)+'\n');process.stdin.flush()
                    if op=='stop':process.wait(timeout=20);reader.join(timeout=2);break
    finally:
        api.release.set()
        if process is not None and process.poll() is None:
            try:process.stdin.write('{"op":"stop"}\n');process.stdin.flush();process.wait(timeout=20)
            except (BrokenPipeError,subprocess.TimeoutExpired):process.terminate();process.wait(timeout=10)
        api.shutdown();api.server_close()
if __name__=='__main__':main()
