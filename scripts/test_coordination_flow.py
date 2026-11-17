"""Isolated scan-to-executor integration, restart recovery and explicit provenance loops."""
from copy import deepcopy
import json
from pathlib import Path
import sys
import tempfile
import unittest
from coordination_checkpoint import Checkpoints
from coordination_executor import Executor
from coordination_flow import ingest


class FlowTests(unittest.TestCase):
    def setUp(self):
        temp = tempfile.TemporaryDirectory(); self.addCleanup(temp.cleanup)
        self.path = Path(temp.name) / 'checkpoints.db'
        self.store = Checkpoints(self.path); self.addCleanup(self.store.close)
        self.event = dict(id='stable-observation', repo='owner/repo', branch='explicit-source', before='a'*40, after='b'*40)
        self.route = dict(repository='owner/repo', branch='explicit-source', sourcePlatform='windows',
                          targetPlatform='macos', mappingVerified=True, mappingEvidence='ISOLATED FIXTURE')
        self.provenance = dict(sourceEventId=self.event['id'], sourceCommit=self.event['after'], originChangeId='fixture-origin',
                               kind='source-change', visitedPlatforms=['windows'], verified=True, evidence='ISOLATED FIXTURE')
        self.binding = dict(commandPrefix=[sys.executable], expectedRule='fixed-probe', expectedAgent='fixed-agent',
                            eventSource='local', eventType='fixture.event', targetPlatform='macos',
                            bindingVerified=True, bindingEvidence='ISOLATED FIXTURE')
        self.count = 0; self.visible = True; self.runstate = 'done'; self.fail = False
        self.executor = Executor(self.binding, self.store, self.invoke)

    def invoke(self, args):
        if args[0] == 'emit':
            self.count += 1; self.subject = args[args.index('--subject')+1]
            if self.fail: raise TimeoutError('simulated response interruption')
            return 'fixture emit'
        runs = [dict(id=17, rule='fixed-probe', agent='fixed-agent', state=self.runstate,
                     events=[dict(source='local', type='fixture.event', subject=self.subject)])] if self.visible else []
        return json.dumps(dict(runs=runs, before=None))

    def test_event_to_run_completion_and_repeat_after_restart(self):
        first = ingest(self.event, self.route, self.provenance, self.executor, 'fixture')
        self.assertEqual(first['execution']['state'], 'done')
        self.assertFalse(first['developmentCompletionAsserted'])
        reopened = Checkpoints(self.path); self.addCleanup(reopened.close)
        second = ingest(self.event, self.route, self.provenance, Executor(self.binding, reopened, self.invoke), 'next-process')
        self.assertEqual(second, first)
        self.assertEqual(self.count, 1)

    def test_unknown_provenance_or_route_never_emits(self):
        for route, provenance in [(dict(self.route, branch='wrong'), self.provenance),
                                  (dict(self.route, mappingVerified=False), self.provenance), (self.route, {}),
                                  (self.route, dict(self.provenance, sourceCommit='c'*40))]:
            with self.assertRaises(ValueError): ingest(self.event, route, provenance, self.executor, 'fixture')
        self.assertEqual(self.count, 0)

    def test_response_loss_queries_existing_run_no_resend(self):
        self.fail = True
        with self.assertRaises(TimeoutError): ingest(self.event, self.route, self.provenance, self.executor, 'fixture')
        self.fail = False
        result = ingest(self.event, self.route, self.provenance, self.executor, 'recovery')
        self.assertEqual(result['execution']['state'], 'done')
        self.assertEqual(self.count, 1)

    def test_in_progress_not_visible_then_cancelled_is_not_success(self):
        self.visible = False
        result = ingest(self.event, self.route, self.provenance, self.executor, 'fixture')
        self.assertEqual(result['handoffState'], 'uncertain'); self.assertIsNone(result['execution'])
        self.visible = True; self.runstate = 'cancelled'
        result = self.executor.track(result['delivery'])
        self.assertEqual(result['execution']['state'], 'cancelled')
        self.assertFalse(result['developmentCompletionAsserted']); self.assertEqual(self.count, 1)

    def test_confirm_before_observation_interruption_is_recoverable(self):
        from coordination_flow import envelope_from_event
        row = self.executor.deliver(envelope_from_event(self.event, self.route, self.provenance), 'fixture')
        self.assertEqual(row['state'], 'confirmed'); self.assertIsNone(self.store.observation(row['id']))
        self.assertEqual(self.executor.track(row['id'])['execution']['state'], 'done')
        self.assertEqual(self.count, 1)

    def test_explicit_alignment_return_is_suppressed_not_reanalyzed(self):
        parent = ingest(self.event, self.route, self.provenance, self.executor, 'fixture')
        route = dict(self.route, sourcePlatform='macos', targetPlatform='windows', branch='explicit-return')
        event = dict(self.event, id='return-observation', branch='explicit-return', before='b'*40, after='c'*40)
        provenance = dict(self.provenance, sourceEventId=event['id'], sourceCommit=event['after'], kind='alignment-result',
                          parentDeliveryId=parent['delivery'], visitedPlatforms=['windows', 'macos'])
        binding = dict(self.binding, targetPlatform='windows')
        result = ingest(event, route, provenance, Executor(binding, self.store, self.invoke), 'return')
        self.assertEqual(result['handoffState'], 'suppressed-loop'); self.assertEqual(self.count, 1)

    def test_terminal_status_cannot_silently_change(self):
        result = ingest(self.event, self.route, self.provenance, self.executor, 'fixture')
        self.runstate = 'cancelled'
        with self.assertRaises(ValueError): self.executor.track(result['delivery'])


if __name__ == '__main__':
    unittest.main()
