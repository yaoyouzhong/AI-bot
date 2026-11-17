"""Local fixtures only; no remote writes, native builds, or releases."""
from copy import deepcopy
from unittest.mock import patch
import unittest
import subprocess
import tempfile
import cross_platform_coordination as c

A, B, C = 'a' * 40, 'b' * 40, 'c' * 40


class CoordinationTests(unittest.TestCase):
    def setUp(self):
        scope = patch.object(c, 'verify_scope', return_value={'fixtureOnly': True})
        scope.start(); self.addCleanup(scope.stop)
        self.task = c.new_task('owner/AI-bot', 'feature', A, B, 'Desktop change')
        self.receipts = [dict(schemaVersion=1, taskId=self.task['taskId'], sourceCommit=B,
            platform=p, status='completed', implementationCommit=C,
            evidence='native-log.txt', reason='Implemented corresponding feature',
            verification=dict(platform=p, result='passed', commit=C, evidence='native-log.txt'))
            for p in c.PLATFORMS]
        self.plan = dict(schemaVersion=1, releaseSource=C, repository='owner/AI-bot',
            scopeEvidence='Reviewed changes since previous release',
            requiredTaskIds=[self.task['taskId']], tasks=[self.task], receipts=self.receipts)

    def test_identity_deterministic_and_tamper_detected(self):
        self.assertEqual(self.task, c.new_task('owner/AI-bot', 'feature', A, B, 'Desktop change'))
        self.task['summary'] = 'different scope'
        with self.assertRaisesRegex(ValueError, 'identity'):
            c.validate_task(self.task)

    @patch.object(c, 'ancestor', return_value=True)
    def test_ready_without_publish(self, _):
        self.assertFalse(c.gate(self.plan, C, '.')['publishPerformed'])

    def test_receipt_rejections(self):
        for key, value in [('sourceCommit', A), ('taskId', 'other'), ('platform', 'linux'),
                           ('status', 'pending'), ('status', 'failed'), ('evidence', ''), ('reason', '')]:
            with self.subTest(key=key, value=value):
                receipt = deepcopy(self.receipts[0]); receipt[key] = value
                with self.assertRaises(ValueError):
                    c.validate_receipt(self.task, receipt)

    def test_existing_acceptance_can_be_reused(self):
        receipt = deepcopy(self.receipts[0])
        receipt['status'] = 'already-present'
        receipt['verification'].update(result='reused', commit=A, reuseReason='Only docs changed')
        c.validate_receipt(self.task, receipt)
        del receipt['verification']['reuseReason']
        with self.assertRaises(ValueError):
            c.validate_receipt(self.task, receipt)

    def test_native_must_match_platform_and_implementation(self):
        for field, value in [('platform', 'macos'), ('commit', A), ('result', 'failed')]:
            receipt = deepcopy(self.receipts[0]); receipt['verification'][field] = value
            with self.assertRaises(ValueError):
                c.validate_receipt(self.task, receipt)

    @patch.object(c, 'ancestor', return_value=True)
    def test_gate_rejects_incomplete_stale_duplicate(self, _):
        cases = []
        p = deepcopy(self.plan); p['receipts'].pop(); cases.append(p)
        p = deepcopy(self.plan); p['receipts'].append(p['receipts'][0]); cases.append(p)
        p = deepcopy(self.plan); p['tasks'] = []; cases.append(p)
        p = deepcopy(self.plan); p['releaseSource'] = A; cases.append(p)
        p = deepcopy(self.plan); p['scopeEvidence'] = ''; cases.append(p)
        p = deepcopy(self.plan); p['requiredTaskIds'] = []; cases.append(p)
        for plan in cases:
            with self.assertRaises(ValueError):
                c.gate(plan, C, '.')

    @patch.object(c, 'ancestor', side_effect=[True, False])
    def test_unmerged_change_blocks(self, _):
        with self.assertRaisesRegex(ValueError, 'absent'):
            c.gate(self.plan, C, '.')

    def test_real_git_ancestry_and_missing_commit(self):
        with tempfile.TemporaryDirectory() as folder:
            def git(*args):
                return subprocess.check_output(['git', '-C', folder, *args], stderr=subprocess.DEVNULL).decode().strip()
            git('init')
            git('-c', 'user.name=Fixture', '-c', 'user.email=fixture@example.invalid', 'commit', '--allow-empty', '-m', 'base')
            base = git('rev-parse', 'HEAD')
            git('-c', 'user.name=Fixture', '-c', 'user.email=fixture@example.invalid', 'commit', '--allow-empty', '-m', 'head')
            head = git('rev-parse', 'HEAD')
            self.assertTrue(c.ancestor(folder, base, head))
            self.assertFalse(c.ancestor(folder, head, base))
            with self.assertRaisesRegex(ValueError, 'unavailable'):
                c.ancestor(folder, A, head)

    @patch.object(c, 'ancestor', return_value=True)
    def test_empty_scope_cannot_bypass_gate(self, _):
        for ids, tasks, receipts in [([], [], []), ([], [self.task], []),
                                      ([self.task['taskId']], [], []),
                                      ([self.task['taskId'], 'omitted'], [self.task], self.receipts)]:
            plan = deepcopy(self.plan)
            plan.update(requiredTaskIds=ids, tasks=tasks, receipts=receipts)
            with self.assertRaises(ValueError):
                c.gate(plan, C, '.')

    @patch.object(c, 'ancestor', return_value=False)
    def test_divergent_task_range_blocks(self, _):
        with self.assertRaisesRegex(ValueError, 'divergent'):
            c.gate(self.plan, C, '.')

    def test_reuse_requires_baseline_in_implementation_history(self):
        self.receipts[0]['verification'].update(result='reused', commit=A, reuseReason='Unchanged behavior')
        with patch.object(c, 'ancestor', side_effect=[True, True, True, False]):
            with self.assertRaisesRegex(ValueError, 'implementation history'):
                c.gate(self.plan, C, '.')
        with patch.object(c, 'ancestor', return_value=True):
            self.assertEqual(c.gate(self.plan, C, '.')['status'], 'ready')

    def test_read_only_scope_never_counts_as_release_completion(self):
        for fields in [{'kind': 'read-only-parity-review'}, {'constraints': {'inspectionOnly': True}}]:
            task = deepcopy(self.task); task.update(fields); task['taskId'] = c.identity(task)
            receipt = deepcopy(self.receipts[0]); receipt['taskId'] = task['taskId']
            with self.assertRaisesRegex(ValueError, 'Read-only'):
                c.validate_receipt(task, receipt)

    def test_boolean_schema_not_accepted_as_integer_version(self):
        self.task['schemaVersion'] = True; self.task['taskId'] = c.identity(self.task)
        with self.assertRaisesRegex(ValueError, 'schema'):
            c.validate_task(self.task)

    def test_not_applicable_still_needs_scope_evidence(self):
        receipt = deepcopy(self.receipts[0]); receipt['status'] = 'not-applicable'
        del receipt['verification']
        c.validate_receipt(self.task, receipt)
        receipt['reason'] = ''
        with self.assertRaises(ValueError):
            c.validate_receipt(self.task, receipt)


if __name__ == '__main__':
    unittest.main()
