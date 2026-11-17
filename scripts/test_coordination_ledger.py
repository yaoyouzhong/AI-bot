"""Isolated lifecycle and release gate integration; no dispatch or publication."""
from copy import deepcopy
from pathlib import Path
from unittest.mock import patch
import unittest
import coordination_ledger as ledger
import cross_platform_coordination as c
import test_cross_platform_coordination as fixtures
C = fixtures.C


class LedgerTests(unittest.TestCase):
    def setUp(self):
        fixture = fixtures.CoordinationTests(); fixture.setUp()
        self.task, self.receipts = fixture.task, fixture.receipts
        self.ledger = ledger.create(self.task, 'Fixture authorization, not a live assignment')

    def step(self, platform, action, receipt=None, claim='worker-fixture'):
        self.ledger = ledger.transition(self.ledger, self.ledger['revision'], platform, action,
                                        claim, 'Fixture evidence only', receipt)

    @patch.object(c, 'ancestor', return_value=True)
    def test_complete_lifecycle_to_gate(self, _):
        for platform, receipt in zip(c.PLATFORMS, self.receipts):
            for action in ('claim', 'acknowledge', 'start', 'block', 'resume'):
                self.step(platform, action)
            self.step(platform, 'finish', receipt)
        plan = ledger.plan([self.ledger], C, 'Complete fixture scope')
        self.assertEqual(c.gate(plan, C, '.')['status'], 'ready')

    def test_claim_cas_and_wrong_worker(self):
        self.step('macos', 'claim')
        with self.assertRaisesRegex(ValueError, 'Stale revision'):
            ledger.transition(self.ledger, 0, 'macos', 'claim', 'other', 'test')
        with self.assertRaisesRegex(ValueError, 'Another worker'):
            self.step('macos', 'acknowledge', claim='other')
        with self.assertRaisesRegex(ValueError, 'Invalid lifecycle'):
            self.step('macos', 'claim')

    def test_unacknowledged_cannot_finish(self):
        self.step('windows', 'claim')
        with self.assertRaisesRegex(ValueError, 'Invalid lifecycle'):
            self.step('windows', 'finish', self.receipts[0])

    def test_pending_cannot_make_plan(self):
        with self.assertRaisesRegex(ValueError, 'not complete'):
            ledger.plan([self.ledger], C, 'scope')

    def test_transport_history_not_real_task(self):
        path = Path(__file__).resolve().parents[1] / 'docs/development/coordination-ledger/transport-probe-v1.json'
        if path.exists():
            with self.assertRaisesRegex(ValueError, 'Not a real'):
                ledger.validate(c.read(path))
        task = deepcopy(self.task); task['simulationOnly'] = True; task['taskId'] = c.identity(task)
        with self.assertRaisesRegex(ValueError, 'Simulation'):
            ledger.create(task, 'authorization')
        with self.assertRaisesRegex(ValueError, 'Simulation'):
            c.validate_receipt(task, self.receipts[0])
        receipt = deepcopy(self.receipts[0]); receipt['simulationOnly'] = True
        with self.assertRaisesRegex(ValueError, 'Simulation'):
            c.validate_receipt(self.task, receipt)

    def test_terminal_cannot_restart(self):
        for action in ('claim', 'acknowledge'):
            self.step('windows', action)
        self.step('windows', 'finish', self.receipts[0])
        with self.assertRaisesRegex(ValueError, 'Invalid lifecycle'):
            self.step('windows', 'start')

    def test_actual_read_only_pilot_cannot_start_or_finish(self):
        directory = Path(__file__).resolve().parents[1] / 'docs/development/coordination-ledger/tasks'
        files = list(directory.glob('*.json'))
        checked = 0
        for path in files:
            data = c.read(path)
            if not c.inspection_only(data['task']):
                continue
            ledger.validate(data)
            checked += 1
            for platform, state in data['platforms'].items():
                if state['status'] == 'acknowledged':
                    for action in ('start', 'finish'):
                        with self.assertRaisesRegex(ValueError, 'Read-only'):
                            ledger.transition(data, data['revision'], platform, action,
                                              state['claimId'], 'fixture only', self.receipts[0])
        self.assertGreater(checked, 0, 'Read-only pilot fixture missing')

    def test_workflow_gates_before_promotion(self):
        workflow = (Path(__file__).resolve().parents[1] / '.github/workflows/promote.yml').read_text()
        self.assertIn('coordination_plan_json:', workflow)
        self.assertLess(workflow.index('coordination.gate('), workflow.index('subprocess.run(command'))
        self.assertIn("plan['repository'] != os.environ['GITHUB_REPOSITORY']", workflow)


if __name__ == '__main__':
    unittest.main()
