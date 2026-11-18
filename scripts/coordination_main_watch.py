"""Main-only native watch. Immutable observation audit; native Hub owns execution state."""
import argparse
import hashlib
import json
import os
from pathlib import Path
import subprocess
from urllib.parse import urlparse
from urllib.request import Request, build_opener, HTTPRedirectHandler, ProxyHandler
from coordination_checkpoint import canonical, require
from coordination_main_review import review_event, alignment_event, sha
from coordination_main_eligibility import binding_digest, schedule_proof, evidence


def immutable(path, value):
    """Exclusive append; partial/changed files fail closed, never silently replace."""
    path.parent.mkdir(parents=True, exist_ok=True)
    encoded=canonical(value)+'\n'
    try:
        with path.open('x',encoding='utf-8') as stream:
            stream.write(encoded); stream.flush(); os.fsync(stream.fileno())
    except FileExistsError:
        require(path.read_text(encoding='utf-8')==encoded,'Immutable observation conflict; inspect before continuing')


def watch_once(config, github, hub, natural_proof=None):
    require(config.get('enabled') is True and config.get('branch')=='main','Explicit main watch required')
    binding=config['binding']; repository=binding['repository']
    require(config.get('baselineVerified') is True,'Verified baseline required')
    require(type(config.get('dispatchEnabled')) is bool,'Explicit dispatch mode required')
    if config['dispatchEnabled']: require(config.get('expectedRule'),'Fixed native rule required')
    root=Path(config['auditRoot']); require(root.is_absolute(),'Fixed absolute audit root required')
    eligibility=config.get('resultEligibility')
    if eligibility:
        require(eligibility.get('enabled') is True and eligibility.get('bindingDigest')==binding_digest(binding), 'Eligibility binding changed')
        require(natural_proof and natural_proof['watcherRule']==eligibility['watcherRule'], 'Natural cron proof required')
    remote=github(repository)
    head=remote.get('sha'); require(sha(head),'Verified GitHub main SHA required')
    if binding.get('mode')=='isolated-alignment':
        require(config.get('workspaceDispatcherVerified') is True,'Dynamic trusted worktree dispatcher not verified')
        payload=alignment_event(repository,'main',head,binding)
    else: payload=review_event(repository,'main',head,binding)
    # Save before dispatch. Reading a current HEAD never marks older heads processed.
    record=dict(schema=1,repository=repository,branch='main',head=head,event=payload)
    if eligibility and not (root/'observations'/(head+'.json')).exists():
        proof_path=root/'main-eligibility'/(head+'.json')
        if proof_path.exists():
            prior=json.loads(proof_path.read_text(encoding='utf-8'))
            require(prior==evidence(binding,head,payload,prior['proof']), 'Partial observation provenance changed')
        else:
            immutable(proof_path,evidence(binding,head,payload,natural_proof))
    immutable(root/'observations'/ (head+'.json'),record)
    if not config['dispatchEnabled']:
        return dict(observedHead=head,state='observed-not-dispatched',reviewedHead=None,alignedHead=None,
                    developmentCompletionAsserted=False,publicationPerformed=False)
    files=sorted((root/'observations').glob('*.json'))
    require(len(files)<=1000,'Observation history needs explicit archival; do not drop pending work')
    results=[]
    for path in files:
        saved=json.loads(path.read_text(encoding='utf-8'))
        require(saved.get('repository')==repository and saved.get('branch')=='main'
                and sha(saved.get('head')) and path.stem==saved['head'],'Untrusted observation path')
        expected=(alignment_event if binding.get('mode')=='isolated-alignment' else review_event)(repository,'main',saved['head'],binding)
        require(saved.get('event')==expected,'Binding or stored event changed; no automatic migration')
        posted=hub('POST','/v1/events',expected)
        require(type(posted.get('event')) is int and posted['event']>0,'Native event acceptance unknown')
        event_id=posted['event']; observed=hub('GET','/v1/events/'+str(event_id))
        require(observed.get('event')==event_id and isinstance(observed.get('runs'),list),'Wrong native event evidence')
        runs=observed['runs']; rule=config['expectedRule']
        require(len(runs)<=1 and all(r.get('rule')==rule for r in runs),'Unexpected native run routing')
        normal=all(x.get('routed')==[rule] and not x.get('paused') for x in (posted,observed))
        recovered=bool(runs) and all(x.get('routed')==[] and x.get('paused')==[rule] for x in (posted,observed))
        state='observed-not-routed' if not(normal or recovered) else (runs[0].get('state') if runs else 'awaiting-run')
        result=dict(head=saved['head'],nativeEvent=event_id,state=state,
                    nativeRun=runs[0].get('run') if runs else None,
                    reviewedHead=None,alignedHead=None,developmentCompletionAsserted=False,publicationPerformed=False)
        digest=hashlib.sha256(canonical(result).encode()).hexdigest()
        immutable(root/'native-observations'/(digest+'.json'),result)
        results.append(result)
    return dict(observedHead=head,results=results)


class NoRedirect(HTTPRedirectHandler):
    def redirect_request(self,*args,**kwargs): raise ValueError('Hub redirects forbidden')


def main():
    parser=argparse.ArgumentParser(description=__doc__); parser.add_argument('--config',required=True)
    args=parser.parse_args(); config=json.loads(Path(args.config).read_text(encoding='utf-8'))
    proof=None
    if config.get('resultEligibility'):
        context=Path(os.environ['AGENT_AUTOLOOP_CONTEXT'])
        proof=schedule_proof(json.loads(context.read_text(encoding='utf-8')),os.environ.get('AGENT_AUTOLOOP_RUN'),config['resultEligibility']['watcherRule'])
    gh=config['ghExecutable']; require(Path(gh).is_absolute(),'Fixed gh executable required')
    def github(repository):
        result=subprocess.run([gh,'api','repos/'+repository+'/commits/main'],capture_output=True,text=True,timeout=60)
        require(result.returncode==0,'GitHub main lookup failed')
        return json.loads(result.stdout)
    if config.get('dispatchEnabled') is False:
        print(json.dumps(watch_once(config,github,None,proof),ensure_ascii=False)); return
    url=config['hubUrl']; parsed=urlparse(url)
    require(parsed.scheme=='http' and parsed.hostname in ('127.0.0.1','::1') and parsed.port
            and parsed.path in ('','/') and not parsed.query and not parsed.fragment and not parsed.username,
            'Only fixed local native Hub endpoint allowed')
    key=os.environ.get('AIBOT_HUB_SOURCE_KEY'); require(bool(key),'Existing source key must be supplied in memory by trusted local launcher')
    opener=build_opener(ProxyHandler({}), NoRedirect())
    def hub(method,path,payload=None):
        request=Request(url.rstrip('/')+path,data=canonical(payload).encode() if payload is not None else None,
                        headers={'Authorization':'Bearer '+key,'Content-Type':'application/json'},method=method)
        with opener.open(request,timeout=30) as response: return json.load(response)
    print(json.dumps(watch_once(config,github,hub,proof),ensure_ascii=False))


if __name__=='__main__':
    try: main()
    except Exception as error:
        raise SystemExit('Main watch blocked: '+type(error).__name__+'; preserve observation and reconcile native state')
