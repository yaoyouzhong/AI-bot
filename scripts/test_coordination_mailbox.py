"""Shared fake GitHub API, separate publisher/consumer and real local checkpoint persistence."""
import base64
import json
from pathlib import Path
import sys
import tempfile
import unittest
from coordination_checkpoint import Checkpoints
from coordination_executor import Executor
from coordination_mailbox import Mailbox


class MailboxTests(unittest.TestCase):
    def setUp(self):
        temp = tempfile.TemporaryDirectory(); self.addCleanup(temp.cleanup)
        self.root = Path(temp.name); self.files = {}; self.writes = 0; self.emits = 0; self.lose_reply = False
        self.config = dict(repository='owner/repo', branch='codex/test-coordination', namespace='isolated-test/test-1',
                           mode='isolated-test', enabled=True, writeAuthorized=True, authorizationEvidence='fixture only', ghExecutable=sys.executable)
        self.route = dict(repository='owner/repo', branch='explicit-source', sourcePlatform='windows', targetPlatform='macos',
                          mappingVerified=True, mappingEvidence='fixture only')
        self.event = dict(id='history-event', repo='owner/repo', branch='explicit-source', before='a'*40, after='b'*40,
                          privateLocalPath='MUST NOT BE PUBLISHED')
        self.provenance = dict(sourceEventId='history-event', sourceCommit='b'*40, originChangeId='isolated-origin',
                               kind='source-change', visitedPlatforms=['windows'], verified=True, evidence='PRIVATE PATH NOT PUBLISHED')
        self.publisher = Mailbox(self.config, self.api)
        self.consumer = Mailbox(self.config, self.api)
        self.store = Checkpoints(self.root/'consumer.db'); self.addCleanup(self.store.close)
        self.binding = dict(commandPrefix=[sys.executable], expectedRule='isolated-rule', expectedAgent='fixture-agent',
                            eventSource='local', eventType='isolated.event', targetPlatform='macos', bindingVerified=True, bindingEvidence='fixture')
        self.executor = Executor(self.binding, self.store, self.cli)

    def api(self, args, payload=None):
        endpoint = args[0]
        if '/git/ref/' in endpoint: return {'object': {'sha': 'c'*40}}
        path = endpoint.split('/contents/', 1)[1].split('?', 1)[0]
        if payload is not None:
            self.writes += 1
            if path in self.files: raise ValueError('409')
            self.files[path] = json.loads(base64.b64decode(payload['content']))
            if self.lose_reply: self.lose_reply = False; raise TimeoutError('committed but response lost')
            return {'commit': {'sha': 'c'*40}}
        if path in self.files:
            return {'encoding': 'base64', 'content': base64.b64encode(json.dumps(self.files[path]).encode()).decode()}
        rows = [{'name': p.rsplit('/',1)[1], 'path': p, 'type': 'file'} for p in self.files if p.startswith(path+'/')]
        return rows if rows else None

    def cli(self, args):
        if args[0] == 'emit':
            self.emits += 1; self.subject = args[args.index('--subject')+1]
            return 'fake emit'
        return json.dumps({'runs': [{'id': 9, 'rule': 'isolated-rule', 'agent': 'fixture-agent', 'state': 'done',
            'events': [{'source': 'local', 'type': 'isolated.event', 'subject': self.subject}]}], 'before': None})

    def test_cross_instance_publish_poll_receipt_and_duplicates(self):
        packet = self.publisher.publish(self.event, self.route, self.provenance)
        self.assertEqual(self.publisher.publish(self.event, self.route, self.provenance)['packetId'], packet['packetId'])
        result = self.consumer.poll(self.route, self.executor, 'fixture')
        self.assertEqual(result['results'][0]['execution']['state'], 'done')
        reopened = Checkpoints(self.root/'consumer.db'); self.addCleanup(reopened.close)
        self.consumer.poll(self.route, Executor(self.binding, reopened, self.cli), 'after-restart')
        self.assertEqual(self.emits, 1); self.assertEqual(self.writes, 2)
        self.assertEqual(len(self.files), 2)
        self.assertNotIn('PRIVATE', json.dumps(self.files)); self.assertNotIn('MUST NOT', json.dumps(self.files))

    def test_lost_github_write_response_reconciles_same_packet(self):
        self.lose_reply = True
        self.assertTrue(self.publisher.publish(self.event, self.route, self.provenance)['reconciled'])
        self.publisher.publish(self.event, self.route, self.provenance)
        self.assertEqual(self.writes, 1)

    def test_packet_tampering_blocks_executor(self):
        self.publisher.publish(self.event, self.route, self.provenance)
        next(iter(self.files.values()))['event']['after'] = 'd'*40
        with self.assertRaises(ValueError): self.consumer.poll(self.route, self.executor, 'fixture')
        self.assertEqual(self.emits, 0)

    def test_main_and_production_mode_forbidden(self):
        for config in [dict(self.config, branch='main'), dict(self.config, mode='production'), dict(self.config, enabled=False)]:
            with self.assertRaises(ValueError): Mailbox(config, self.api)
        self.assertEqual(self.writes, 0)

    def test_missing_namespace_is_empty_only_with_existing_branch(self):
        self.assertEqual(self.consumer.poll(self.route, self.executor, 'fixture')['results'], [])
        self.assertEqual(self.emits, 0)


if __name__ == '__main__': unittest.main()
