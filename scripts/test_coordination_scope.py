"""Real isolated Git histories: independent scope pins, omitted tasks and source diffs."""
from copy import deepcopy
import json
import os
from pathlib import Path
import subprocess
import tempfile
import unittest
from unittest.mock import patch
import coordination_scope as scope
import cross_platform_coordination as c


class ScopeTests(unittest.TestCase):
    def setUp(self):
        temp = tempfile.TemporaryDirectory(); self.addCleanup(temp.cleanup)
        self.root = Path(temp.name)
        self.git('init')
        self.git('config', 'user.name', 'Fixture')
        self.git('config', 'user.email', 'fixture@example.invalid')
        self.git('commit', '--allow-empty', '-m', 'baseline')
        self.base = self.git('rev-parse', 'HEAD')
        (self.root / 'first.txt').write_text('first\n'); self.git('add', '.')
        self.git('commit', '-m', 'first task'); middle = self.git('rev-parse', 'HEAD')
        (self.root / 'second.txt').write_text('second\n'); self.git('add', '.')
        self.git('commit', '-m', 'second task'); self.source = self.git('rev-parse', 'HEAD')
        tasks = [c.new_task('owner/repo', 'fixture', self.base, middle, 'First change'),
                 c.new_task('owner/repo', 'fixture', middle, self.source, 'Second change')]
        ids = [t['taskId'] for t in tasks]
        receipts = [dict(schemaVersion=1, taskId=t['taskId'], sourceCommit=t['sourceCommit'],
                        platform=p, status='completed', implementationCommit=self.source,
                        reason='Fixture only', evidence='Fixture only',
                        verification=dict(platform=p, result='passed', commit=self.source, evidence='Fixture only'))
                    for t in tasks for p in c.PLATFORMS]
        self.plan = dict(schemaVersion=1, repository='owner/repo', releaseSource=self.source,
                         scopeEvidence='Fixture only', requiredTaskIds=ids, tasks=tasks, receipts=receipts)
        self.record = scope.inventory(self.root, 'owner/repo', self.base, self.source)
        self.record.update(reviewed=True, scopeEvidence='Fixture reviewed scope', requiredTaskIds=ids,
                           commitTasks={middle: [ids[0]], self.source: [ids[1]]},
                           fileTasks={'first.txt': [ids[0]], 'second.txt': [ids[1]]})
        self.pin = self.save(self.record)
        env = patch.dict(os.environ, {'AIBOT_COORDINATION_SCOPE_COMMIT': self.pin})
        env.start(); self.addCleanup(env.stop)

    def git(self, *args):
        return subprocess.check_output(['git', '-C', str(self.root), *args], stderr=subprocess.DEVNULL).decode().strip()

    def save(self, record):
        path = self.root / scope.record_path(self.source)
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text(json.dumps(record))
        self.git('add', '.'); self.git('commit', '--allow-empty', '-m', 'reviewed scope fixture')
        return self.git('rev-parse', 'HEAD')

    def test_actual_history_and_both_tasks_pass(self):
        report = c.gate(self.plan, self.source, self.root)
        self.assertEqual(report['scope']['commit'], self.pin)
        self.assertEqual(report['scope']['commitCount'], 2)
        self.assertEqual(report['scope']['fileCount'], 2)

    def test_removing_entire_task_from_plan_is_detected(self):
        self.plan['requiredTaskIds'].pop()
        self.plan['tasks'].pop()
        self.plan['receipts'] = self.plan['receipts'][:2]
        with self.assertRaisesRegex(ValueError, 'independently pinned scope'):
            c.gate(self.plan, self.source, self.root)

    def test_empty_registry_fails_even_in_scope_record(self):
        record = deepcopy(self.record); record['requiredTaskIds'] = []
        os.environ['AIBOT_COORDINATION_SCOPE_COMMIT'] = self.save(record)
        with self.assertRaisesRegex(ValueError, 'Empty scope'):
            c.gate(self.plan, self.source, self.root)

    def test_missing_actual_commit_file_or_mapping_blocks(self):
        for field in ('commits', 'files', 'commitTasks', 'fileTasks'):
            with self.subTest(field=field):
                record = deepcopy(self.record)
                value = record[field]
                if isinstance(value, list): value.pop()
                else: value.pop(next(iter(value)))
                os.environ['AIBOT_COORDINATION_SCOPE_COMMIT'] = self.save(record)
                with self.assertRaises(ValueError): c.gate(self.plan, self.source, self.root)

    def test_plan_cannot_override_independent_pin(self):
        self.plan['scopeCommit'] = 'f' * 40
        self.assertEqual(c.gate(self.plan, self.source, self.root)['scope']['commit'], self.pin)
        with patch.dict(os.environ, {}, clear=True):
            with self.assertRaisesRegex(ValueError, 'must pin'):
                c.gate(self.plan, self.source, self.root)

    def test_unreviewed_scope_cannot_release(self):
        record = deepcopy(self.record); record['reviewed'] = False
        os.environ['AIBOT_COORDINATION_SCOPE_COMMIT'] = self.save(record)
        with self.assertRaisesRegex(ValueError, 'not reviewed'):
            c.gate(self.plan, self.source, self.root)

    def test_working_tree_edit_cannot_change_committed_scope(self):
        path = self.root / scope.record_path(self.source)
        path.write_text('{}')
        self.assertEqual(c.gate(self.plan, self.source, self.root)['status'], 'ready')

    def test_base_must_precede_source(self):
        with self.assertRaises(ValueError):
            scope.inventory(self.root, 'owner/repo', self.source, self.base)


if __name__ == '__main__':
    unittest.main()
