"""Local natural-watch provenance plus GitHub ancestry qualification. No writes to GitHub."""
from datetime import datetime
import hashlib
import json
import re
from coordination_checkpoint import canonical, require


def full_sha(value):
    return isinstance(value,str) and re.fullmatch('[0-9a-f]{40}',value) is not None


def binding_digest(binding):
    return hashlib.sha256(canonical(binding).encode()).hexdigest()


def utc_time(value):
    require(isinstance(value,str) and re.fullmatch(r'\d{4}-\d\d-\d\dT\d\d:\d\d:\d\d(?:\.\d{1,6})?Z',value), 'Invalid native schedule time')
    return datetime.fromisoformat(value.replace('Z','+00:00'))


def schedule_proof(context, run, rule):
    require(isinstance(context,dict) and not any(context.get(k) for k in ('answers','timers','waiting','open')), 'Not a pure schedule context')
    events=context.get('events')
    require(isinstance(events,list) and 1<=len(events)<=1000, 'Bounded natural schedule events required')
    require(isinstance(run,str) and re.fullmatch('[1-9][0-9]*',run),'Native watcher run required')
    ticks={}
    for event in events:
        require(isinstance(event,dict) and event.get('source')=='schedule' and event.get('type')=='schedule.tick'
                and type(event.get('hops')) is int and event['hops']==0, 'Not the fixed native cron')
        identity=event.get('id')
        require(isinstance(identity,str) and re.fullmatch(re.escape(rule)+r'@\d{4}-\d\d-\d\dT\d\d:\d\d:\d\d\.\d{3}Z',identity), 'Invalid schedule identity')
        utc_time(identity[len(rule)+1:]);received=event.get('receivedAt');utc_time(received)
        item=dict(scheduleId=identity,receivedAt=received)
        require(identity not in ticks or ticks[identity]==item, 'Conflicting duplicate schedule event')
        ticks[identity]=item
    ordered=sorted(ticks.values(),key=lambda x:(utc_time(x['receivedAt']),x['scheduleId']))
    # Latest wake-up permits a retry that also carries older ticks. The GitHub
    # anchor still excludes pre-authorization source changes. Keep every tick.
    latest=ordered[-1]
    proof=dict(watcherRule=rule,watcherRun=int(run),**latest)
    if len(ordered)>1:proof.update(scheduleProofVersion=2,scheduleEvents=ordered)
    return proof


def validate_schedule_proof(proof, rule):
    require(isinstance(proof,dict), 'Invalid stored schedule proof')
    if 'scheduleProofVersion' in proof:
        require(type(proof['scheduleProofVersion']) is int and proof['scheduleProofVersion']==2, 'Unknown schedule proof version')
        ticks=proof.get('scheduleEvents');require(isinstance(ticks,list) and 2<=len(ticks)<=1000,'Invalid merged schedule proof')
        require(all(isinstance(t,dict) and set(t)=={'scheduleId','receivedAt'} for t in ticks),'Unexpected schedule proof fields')
    else:
        ticks=[dict(scheduleId=proof.get('scheduleId'),receivedAt=proof.get('receivedAt'))]
    context=dict(events=[dict(id=t['scheduleId'],receivedAt=t['receivedAt'],source='schedule',type='schedule.tick',hops=0) for t in ticks])
    expected=schedule_proof(context,str(proof.get('watcherRun')),rule)
    require(expected==proof,'Noncanonical or altered schedule proof')
    return proof


def evidence(binding, head, event, proof):
    return dict(schemaVersion=1,repository=binding['repository'],branch='main',sourceCommit=head,
                bindingDigest=binding_digest(binding),eventKey=event['id'],trigger='native-schedule',proof=proof)


def descendant(github, base, head):
    require(full_sha(base) and full_sha(head),'Full ancestry SHAs required')
    result=github('compare/'+base+'...'+head)
    require(result.get('base_commit',{}).get('sha')==base
            and result.get('merge_base_commit',{}).get('sha')==base
            and result.get('status') in ('ahead','identical'), 'Commit is not in the authorized main lineage')
