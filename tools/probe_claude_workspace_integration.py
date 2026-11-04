"""Exercise the native Swift workspace against the installed official CLI.
Temporary HOME, a dummy key, synthetic sessions and loopback API only.
"""
import json,os,shutil,subprocess,tempfile,threading,uuid
from pathlib import Path
from http.server import BaseHTTPRequestHandler,ThreadingHTTPServer
class API(BaseHTTPRequestHandler):
    def log_message(self,*a):pass
    def do_POST(self):
        data=json.loads(self.rfile.read(int(self.headers.get('Content-Length','0'))));self.server.calls+=1
        messages=data.get('messages',[]);latest=next((v.get('content','') for v in reversed(messages) if v.get('role')=='user'),'')
        slow='NATIVE_SLOW' in str(latest)
        print('SYNTHETIC_API_CALL',self.server.calls,'slow='+str(slow),flush=True)
        if slow:self.server.release.wait(30)
        body=json.dumps({'id':'msg_native_'+str(self.server.calls),'type':'message','role':'assistant','model':'claude-sonnet-4-6','content':[{'type':'text','text':'Native workspace synthetic public reply'}],'stop_reason':'end_turn','stop_sequence':None,'usage':{'input_tokens':5,'output_tokens':2}}).encode()
        try:self.send_response(200);self.send_header('Content-Type','application/json');self.send_header('Content-Length',str(len(body)));self.end_headers();self.wfile.write(body)
        except (BrokenPipeError,ConnectionResetError):pass

def main():
    cli=shutil.which('claude');assert cli
    api=ThreadingHTTPServer(('127.0.0.1',0),API);api.calls=0;api.release=threading.Event();threading.Thread(target=api.serve_forever,daemon=True).start()
    try:
        with tempfile.TemporaryDirectory(prefix='aibot-native-claude-') as directory:
            root=Path(directory);(root/'.claude.json').write_text(json.dumps({'hasCompletedOnboarding':True}))
            env={'PATH':'/usr/bin:/bin:/usr/sbin:/sbin','HOME':directory,'TMPDIR':directory,'ANTHROPIC_BASE_URL':f'http://127.0.0.1:{api.server_port}','ANTHROPIC_API_KEY':'synthetic-key','CLAUDE_CODE_DISABLE_NONESSENTIAL_TRAFFIC':'1','DISABLE_TELEMETRY':'1','DISABLE_ERROR_REPORTING':'1'}
            args=['--model','claude-sonnet-4-6','--setting-sources','user','--strict-mcp-config','--tools','','--disable-slash-commands','--max-budget-usd','0.05']
            seed=subprocess.run([cli,'--print','--output-format','json','--session-id='+str(uuid.uuid4()),*args,'Synthetic seed session'],env=env,cwd=directory,stdout=subprocess.PIPE,stderr=subprocess.DEVNULL,timeout=30)
            assert seed.returncode==0
            (root/'environment.json').write_text(json.dumps(env));(root/'arguments.json').write_text(json.dumps(args))
            testenv=os.environ.copy();testenv['AIBOT_CLAUDE_NATIVE_ROOT']=directory;testenv['AIBOT_CLAUDE_NATIVE_CLI']=cli
            package=Path(__file__).resolve().parents[1]/'mac-app'
            run=subprocess.run(['swift','test','--skip-build','--package-path',str(package),'--filter','MacClaudeWorkspaceTests.testRealCLIWorkspaceLoopback'],env=testenv,stdout=subprocess.PIPE,stderr=subprocess.STDOUT,text=True,timeout=80)
            print(run.stdout);assert run.returncode==0
            print('NATIVE_CLAUDE_WORKSPACE_INTEGRATION_OK api_calls='+str(api.calls)+' synthetic_text_image_resume_queue_interrupt_only')
    finally:api.release.set();api.shutdown();api.server_close()
if __name__=='__main__':main()
