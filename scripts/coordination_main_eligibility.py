"""Local natural-watch provenance plus GitHub ancestry qualification. No writes to GitHub."""
import hashlib
import json
import re
from coordination_checkpoint import canonical, require


def full_sha(value):
    return isinstance(value,str) and re.fullmatch('[0-9a-f]{40}',value) is not None


def binding_digest(binding):
    return hashlib.sha256(canonical(binding).encode()).hexdigest()


def schedule_proof(context, run, rule):
    require(isinstance(context,dict) and not any(context.get(k) for k in ('answers','timers','waiting','open')), 'Not a fresh schedule context')
    events=context.get('events');require(isinstance(events,list) and len(events)==1,'Single natural schedule event required')
    event=events[0]
    require(event.get('source')=='schedule' and event.get('type')=='schedule.tick'
            and event.get('hops')==0 and event.get('id','').startswith(rule+'@'), 'Not the fixed native cron')
    require(re.fullmatch(re.escape(rule)+r'@\d{4}-\d\d-\d\dT\d\d:\d\d:\d\d\.\d{3}Z',event['id']), 'Invalid schedule identity')
    require(isinstance(run,str) and re.fullmatch('[1-9][0-9]*',run),'Native watcher run required')
    return dict(watcherRule=rule,watcherRun=int(run),scheduleId=event['id'],receivedAt=event['receivedAt'])


def evidence(binding, head, event, proof):
    return dict(schemaVersion=1,repository=binding['repository'],branch='main',sourceCommit=head,
                bindingDigest=binding_digest(binding),eventKey=event['id'],trigger='native-schedule',proof=proof)


def descendant(github, base, head):
    require(full_sha(base) and full_sha(head),'Full ancestry SHAs required')
    result=github('compare/'+base+'...'+head)
    require(result.get('base_commit',{}).get('sha')==base
            and result.get('merge_base_commit',{}).get('sha')==base
            and result.get('status') in ('ahead','identical'), 'Commit is not in the authorized main lineage')
