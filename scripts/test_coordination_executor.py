"""Fixed CLI contract fixtures only; real Hub probes are separately reported."""
from copy import deepcopy
import json
from pathlib import Path
import sys
import tempfile
import unittest
from coordination_checkpoint import Checkpoints
from coordination_executor import Executor


class ExecutorTests(unittest.TestCase):
    def setUp(self):
        temp = tempfile.TemporaryDirectory(); self.addCleanup(temp.cleanup)
        self.store = Checkpoints(Path(temp.name) / 'state.db'); self.addCleanup(self.store.close)
        self.binding = dict(commandPrefix=[sys.executable, 'unused-fixture'], expectedRule='probe',
                            expectedAgent='probe-agent', eventSource='local', eventType='probe.event',
                            targetPlatform='macos', bindingVerified=True, bindingEvidence='isolated fixture')
        self.envelope = dict(repository='owner/repo', originChangeId='fixture', sourceReference='fixture-ref',
                             sourcePlatform='windows', targetPlatform='macos', kind='source-change', visitedPlatforms=['windows'])
        self.emits = 0; self.visible = True; self.timeout = False
        self.executor = Executor(self.binding, self.store, self.invoke)

    def invoke(self, args):
        if args[0] == 'emit':
            self.emits += 1; self.subject = args[args.index('--subject') + 1]
            self.assertEqual(self.subject, args[args.index('--id') + 1])
            if self.timeout: raise TimeoutError('fixture response lost')
            return '中文成功摘要不能作为回执'
        rows = [dict(id=42, rule='probe', agent='probe-agent', state='done',
                     events=[dict(source='local', type='probe.event', subject=self.subject)])] if self.visible else []
        return json.dumps(dict(runs=rows, before=None))

    def test_real_run_shape_confirms_and_repeat_does_not_emit(self):
        first = self.executor.deliver(self.envelope, 'fixture')
        self.assertEqual(first['state'], 'confirmed')
        self.assertEqual(self.executor.deliver(self.envelope, 'fixture')['effect_ref'], 'autoloop:probe:run:42')
        self.assertEqual(self.emits, 1)

    def test_exit_zero_without_run_is_uncertain_then_query_only(self):
        self.visible = False
        row = self.executor.deliver(self.envelope, 'fixture')
        self.assertEqual(row['state'], 'uncertain')
        self.visible = True
        self.assertEqual(self.executor.deliver(self.envelope, 'fixture')['state'], 'confirmed')
        self.assertEqual(self.emits, 1)

    def test_timeout_preserves_id_and_recovers_without_emit(self):
        self.timeout = True
        with self.assertRaises(TimeoutError): self.executor.deliver(self.envelope, 'fixture')
        self.timeout = False
        self.assertEqual(self.executor.deliver(self.envelope, 'fixture')['state'], 'confirmed')
        self.assertEqual(self.emits, 1)

    def test_wrong_target_or_unverified_binding_blocks(self):
        bad = dict(self.envelope, targetPlatform='windows')
        with self.assertRaises(ValueError): self.executor.deliver(bad, 'fixture')
        with self.assertRaises(ValueError): Executor(dict(self.binding, bindingVerified=False), self.store, self.invoke)
        self.assertEqual(self.emits, 0)

    def test_binding_change_cannot_redirect_existing_delivery(self):
        self.visible = False
        row = self.executor.deliver(self.envelope, 'fixture')
        other = Executor(dict(self.binding, expectedRule='other'), self.store, self.invoke)
        with self.assertRaises(ValueError): other.reconcile(row['id'])
        with self.assertRaises(ValueError): other.deliver(self.envelope, 'fixture')
        self.assertEqual(self.emits, 1)

    def test_duplicate_matches_across_pages_fail_closed(self):
        def pages(args):
            return json.dumps(dict(runs=[dict(id=1 if '--before' not in args else 2, rule='probe', agent='probe-agent',
                events=[dict(source='local', type='probe.event', subject='delivery')])],
                before=5 if '--before' not in args else None))
        with self.assertRaisesRegex(ValueError, 'Multiple'):
            Executor(self.binding, self.store, pages).find_run('delivery')


if __name__ == '__main__':
    unittest.main()
