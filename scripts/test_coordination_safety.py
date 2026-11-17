"""CAS/disconnect model and publishing entry-point guard tests. No live writes."""
from copy import deepcopy
import json
import os
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest
from unittest.mock import patch
import coordination_ledger as ledger
import cross_platform_coordination as c
import release_pipeline as pipeline
import product_promotion as product
import test_cross_platform_coordination as fixtures


class SafetyTests(unittest.TestCase):
    def setUp(self):
        fixture = fixtures.CoordinationTests(); fixture.setUp(); self.addCleanup(fixture.doCleanups)
        self.task = fixture.task
        self.state = ledger.create(self.task, 'ISOLATED TEST ONLY')
        self.version = 0

    def cas(self, expected, value, disconnect=False):
        if expected != self.version:
            raise ValueError('409 conflict')
        self.state = deepcopy(value); self.version += 1
        if disconnect:
            raise ConnectionError('response lost after server committed')

    def test_simultaneous_claims_only_one_wins(self):
        first = ledger.transition(self.state, 0, 'macos', 'claim', 'a', 'fixture')
        second = ledger.transition(self.state, 0, 'macos', 'claim', 'b', 'fixture')
        self.cas(0, first)
        with self.assertRaisesRegex(ValueError, '409'):
            self.cas(0, second)
        self.assertEqual(self.state['platforms']['macos']['claimId'], 'a')
        with self.assertRaises(ValueError):
            ledger.transition(self.state, 1, 'macos', 'claim', 'b', 'fixture')

    def test_lost_response_recovered_by_read_not_blind_overwrite(self):
        claimed = ledger.transition(self.state, 0, 'windows', 'claim', 'a', 'fixture')
        with self.assertRaises(ConnectionError):
            self.cas(0, claimed, disconnect=True)
        self.assertEqual(self.state, claimed)
        with self.assertRaises(ValueError):
            self.cas(0, claimed)
        acknowledged = ledger.transition(self.state, 1, 'windows', 'acknowledge', 'a', 'fixture')
        self.cas(1, acknowledged)
        self.assertEqual(self.state['revision'], 2)

    def test_cross_platform_conflict_rebase_preserves_other_claim(self):
        windows = ledger.transition(self.state, 0, 'windows', 'claim', 'win', 'fixture')
        mac = ledger.transition(self.state, 0, 'macos', 'claim', 'mac', 'fixture')
        self.cas(0, windows)
        with self.assertRaises(ValueError): self.cas(0, mac)
        mac = ledger.transition(self.state, 1, 'macos', 'claim', 'mac', 'fixture')
        self.cas(1, mac)
        self.assertEqual(self.state['platforms']['windows']['claimId'], 'win')

    def test_missing_plan_blocks_single_release_before_remote_calls(self):
        with patch.dict(os.environ, {}, clear=True), patch.object(pipeline, 'readiness', return_value=({'sourceCommit': fixtures.C}, {}, [])), patch.object(pipeline, 'run_cli') as remote:
            with self.assertRaisesRegex(ValueError, 'AIBOT_COORDINATION_PLAN'):
                pipeline.promote('unused', 'unused', 'owner/repo', execute=True)
            remote.assert_not_called()

    def test_missing_plan_blocks_product_before_reservation(self):
        candidate = {'sourceCommit': fixtures.C, 'tag': 'bridge-v0.0.0'}
        with patch.dict(os.environ, {}, clear=True), patch.object(product, 'component_plan', return_value={'candidate': candidate}), patch.object(product, 'reserve_product') as reserve:
            with self.assertRaisesRegex(ValueError, 'AIBOT_COORDINATION_PLAN'):
                product.promote_product([{}], 'owner/repo', execute=True)
            reserve.assert_not_called()

    def test_plan_directory_and_repository_check(self):
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / (fixtures.C + '.json')
            path.write_text(json.dumps({'repository': 'owner/repo'}))
            with patch.dict(os.environ, {'AIBOT_COORDINATION_PLAN': directory}), patch.object(c, 'gate', return_value={'status': 'fixture'}) as gate:
                pipeline.require_coordination({'sourceCommit': fixtures.C}, 'owner/repo')
                gate.assert_called_once()
                with self.assertRaisesRegex(ValueError, 'repository differs'):
                    pipeline.require_coordination({'sourceCommit': fixtures.C}, 'another/repo')

    def test_legacy_upload_cli_cannot_bypass_pipeline(self):
        result = subprocess.run([sys.executable, str(Path(__file__).with_name('upload_release_resumable.py')), 'upload', '--repo', 'owner/repo', '--tag', 'v0.0.0', '--directory', '.', '--manifest', 'unused', '--state', 'unused'], capture_output=True, text=True)
        self.assertNotEqual(result.returncode, 0)
        self.assertIn('Standalone release uploads are disabled', result.stderr)


if __name__ == '__main__':
    unittest.main()
