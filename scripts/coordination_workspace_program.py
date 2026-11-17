"""Trusted native program wrapper: validate event metadata, select isolated worktree, exec Codex."""
import argparse
import json
import os
from pathlib import Path
import subprocess
import sys
from coordination_checkpoint import require
from coordination_main_review import alignment_event, sha


def target_head(context, binding, event_source):
    events=context.get('events'); require(isinstance(events,list) and events,'No source event; do not guess resume workspace')
    heads=set()
    for event in events:
        require(event.get('source')==event_source and event.get('type')==binding['eventType'],'Unexpected native event source/type')
        detail=json.loads(event['detail']); head=detail.get('sourceCommit')
        require(sha(head),'Missing exact main SHA')
        expected=alignment_event(binding['repository'],'main',head,binding)
        require(all(event.get(k)==expected[k] for k in ('id','type','subject','detail','link')),'Native task metadata differs from fixed binding')
        heads.add(head)
    require(len(heads)==1,'Multiple main snapshots cannot share a worktree session')
    return heads.pop()


def prepare(config, context, runner=None):
    binding=config['binding']; head=target_head(context,binding,config['eventSource'])
    require(Path(config['workspaceRoot']).is_absolute() and Path(config['isolatedClone']).is_absolute(),'Absolute workspace paths required')
    root=Path(config['workspaceRoot']).resolve(); repo=Path(config['isolatedClone']).resolve()
    require(root.is_absolute() and repo.is_absolute() and root!=repo,'Separate fixed workspace root required')
    require(config.get('isolatedCloneVerified') is True,'Independent clone must be verified first')
    git=config['gitExecutable']; require(Path(git).is_absolute(),'Fixed Git executable required')
    def invoke(args):
        result=subprocess.run([git,'-C',str(repo),*args],capture_output=True,text=True,timeout=120)
        require(result.returncode==0,'Worktree operation failed; preserve state, no reset')
        return result.stdout.strip()
    run=runner or invoke
    root.mkdir(parents=True,exist_ok=True)
    lock=root/'.prepare-lock'
    try: lock.mkdir()
    except FileExistsError: raise ValueError('Another preparation or interrupted lock exists; do not steal it')
    try:
        require(run(['rev-parse','--is-inside-work-tree'])=='true','Invalid independent clone')
        remote=run(['remote','get-url','origin'])
        require(remote in ('https://github.com/'+binding['repository']+'.git',
                           'https://github.com/'+binding['repository']), 'Unexpected isolated clone remote')
        directory=root/head; marker=root/(head+'.binding.json')
        identity=dict(repository=binding['repository'],head=head,target=binding['targetPlatform'],workspaceRef=binding['workspaceRef'])
        if directory.exists():
            require(marker.is_file() and json.loads(marker.read_text())==identity,'Unverified existing worktree; do not reuse')
            # Query actual worktree without changing its contents, including dirty files.
            result=(runner(['worktree-head',str(directory)]) if runner else
                    subprocess.run([git,'-C',str(directory),'rev-parse','HEAD'],capture_output=True,text=True,timeout=30))
            actual=result if runner else result.stdout.strip()
            require(actual==head,'Worktree HEAD changed; reconcile before resume')
            return directory
        require(not marker.exists(),'Orphaned worktree marker; no silent recreation')
        run(['fetch','origin','main'])
        run(['merge-base','--is-ancestor',head,'origin/main'])
        run(['worktree','add','--detach',str(directory),head])
        with marker.open('x',encoding='utf-8') as stream:
            json.dump(identity,stream,sort_keys=True); stream.flush(); os.fsync(stream.fileno())
        return directory
    finally: lock.rmdir()


def main():
    parser=argparse.ArgumentParser(description=__doc__); parser.add_argument('--config',required=True)
    args,forward=parser.parse_known_args()
    if forward and forward[0]=='--': forward=forward[1:]
    require(forward and forward[0]=='exec','Only native Codex exec supported')
    config=json.loads(Path(args.config).read_text(encoding='utf-8'))
    context_path=Path(os.environ.get('AGENT_AUTOLOOP_CONTEXT',''))
    require(context_path.is_absolute() and context_path.is_file() and context_path.stat().st_size<=1048576,'Valid native context required')
    context=json.loads(context_path.read_text(encoding='utf-8'))
    directory=prepare(config,context)
    executable=config['codexExecutable']; require(Path(executable).is_absolute(),'Fixed Codex executable required')
    # Upstream native worker owns argv/session. Event data never becomes shell/argv.
    if os.name == 'nt':
        # Windows exec is not POSIX process replacement. Keep the supervised
        # Python process alive until Codex exits, preserving inherited stdio.
        completed = subprocess.run([executable,*forward], cwd=str(directory), shell=False)
        raise SystemExit(completed.returncode)
    os.chdir(directory); os.execv(executable,[executable,*forward])


if __name__=='__main__':
    try: main()
    except Exception as error: raise SystemExit('Workspace dispatch blocked: '+type(error).__name__+'; no reset/retry with new identity')
