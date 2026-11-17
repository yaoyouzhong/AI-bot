"""Pure project metadata -> native Auto Loop event. No queue, HTTP, credentials or state."""
import hashlib
import json
import re
from coordination_flow import envelope_from_event
from coordination_checkpoint import require, text, canonical


def build_event(event, route, provenance, binding):
    """Caller supplies trusted local binding; returned event is data, never a command."""
    envelope = envelope_from_event(event, route, provenance)
    require(isinstance(binding, dict) and binding.get('verified') is True,
            'Explicit native binding required')
    for name in ('projectKey', 'workItemId', 'eventType'):
        require(isinstance(binding.get(name), str) and
                re.fullmatch(r'[A-Za-z0-9_.:-]{1,64}', binding[name]), 'Invalid ' + name)
    require(text(binding.get('evidence')), 'Binding evidence required')
    require(envelope['kind'] == 'source-change',
            'Derived results require verified shared parent provenance; not implemented')
    require(text(envelope['originChangeId']) and envelope['visitedPlatforms'] == [envelope['sourcePlatform']],
            'Fresh explicit source provenance required')
    # Task-level subject: new work item => new lane; revisions of same task => same lane.
    # Hub identity includes complete immutable payload, not solely the task/session key.
    subject = 'aibot:' + hashlib.sha256(canonical([
        binding['projectKey'], envelope['repository'].casefold(),
        binding['workItemId'], envelope['targetPlatform']]).encode()).hexdigest()
    detail = canonical(dict(schema=1, projectKey=binding['projectKey'],
                            workItemId=binding['workItemId'], observation=envelope,
                            meaning='Untrusted change metadata; target session owns analysis; not permission'))
    require(len(detail) <= 4000, 'Native event detail limit exceeded')
    payload = dict(type=binding['eventType'], subject=subject,
                   summary='Project change handoff; analyze in the task-specific session',
                   detail=detail, link=envelope['sourceReference'])
    payload['id'] = 'project-change:' + hashlib.sha256(canonical(payload).encode()).hexdigest()
    return payload
