"""Reuse GitHub mailbox I/O; native Hub owns queue/dedup/runs. Isolated-test only."""
import hashlib
import re
from urllib.parse import quote
from coordination_mailbox import Mailbox
from coordination_native_event import build_event
from coordination_checkpoint import canonical, require, text


class NativeMailbox(Mailbox):
    def poll_native(self, route, binding, hub):
        """hub(method,path,payload=None) is the trusted local native-source transport.

        No credentials, HTTP client, database, cursor or custom recovery state here.
        On interruption repeat the same immutable event via native source/id dedup.
        """
        require(route['repository'] == self.config['repository'], 'Wrong mailbox repository')
        packet_id = binding.get('expectedPacketId', '')
        require(re.fullmatch('[0-9a-f]{64}', packet_id) is not None, 'Explicit isolated packet required')
        require(text(binding.get('expectedRule')), 'Fixed native rule required')
        ref = self.runner([self.prefix + 'git/ref/heads/' + quote(self.config['branch'], safe='/')])
        commit = ref['object']['sha']
        require(re.fullmatch('[0-9a-f]{40}', commit) is not None, 'Invalid snapshot')
        packet = self.get_file(self.directory + '/' + packet_id + '.json', commit)
        if packet is None: return dict(snapshot=commit, state='awaiting-packet')
        require(isinstance(packet, dict) and type(packet.get('schemaVersion')) is int
                and packet['schemaVersion'] == 1 and packet.get('mode') == 'isolated-test', 'Invalid packet')
        identity = hashlib.sha256(canonical({k:v for k,v in packet.items() if k != 'packetId'}).encode()).hexdigest()
        require(packet.get('packetId') == packet_id == identity, 'Packet identity mismatch')
        require(packet.get('sourcePlatform') == route['sourcePlatform']
                and packet.get('targetPlatform') == route['targetPlatform'], 'Wrong platform route')
        payload = build_event(packet['event'], route, packet['provenance'], binding)
        posted = hub('POST', '/v1/events', payload)
        require(isinstance(posted, dict) and type(posted.get('event')) is int and posted['event'] > 0,
                'Native event acceptance unknown')
        event_id = posted['event']
        observed = hub('GET', '/v1/events/' + str(event_id))
        require(isinstance(observed, dict) and type(observed.get('event')) is int
                and observed['event'] == event_id
                and isinstance(observed.get('runs'), list), 'Invalid native observation')
        runs = observed['runs']
        require(all(r.get('rule') == binding['expectedRule'] for r in runs) and len(runs) <= 1,
                'Unexpected or multiple native runs; no completion receipt')
        # Native replay retains the historical paused/routed fields. Only a real
        # unique run linked by this event's GET can establish successful recovery.
        normal = all(not x.get('paused') and x.get('routed') == [binding['expectedRule']]
                     for x in (posted, observed))
        recovered = bool(runs) and all(x.get('paused') == [binding['expectedRule']] and x.get('routed') == []
                                      for x in (posted, observed))
        require(normal or recovered, 'Native route still paused/unmatched; explicit native replay required')
        result = dict(snapshot=commit, packetId=packet_id, nativeEvent=event_id,
                      eventKey=payload['id'], state='awaiting-run', recoveredAfterPause=recovered, developmentCompletionAsserted=False,
                      publicationPerformed=False)
        if not runs: return result
        run = runs[0]
        require(type(run.get('run')) is int and run['run'] > 0, 'Invalid native run')
        result.update(state=run.get('state'), nativeRun=run['run'])
        if run.get('state') in ('done', 'nothing', 'cancelled'):
            receipt = dict(schemaVersion=1, mode='isolated-test', transport='native-hub', packetId=packet_id,
                           nativeEvent=event_id, eventKey=payload['id'], nativeRun=run['run'],
                           rule=binding['expectedRule'], targetPlatform=route['targetPlatform'],
                           executorState=run['state'], developmentCompletionAsserted=False, publicationPerformed=False)
            result['receipt'] = self.create(packet_id + '.receipt.json', receipt)
        return result
