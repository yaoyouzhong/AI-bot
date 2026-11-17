"""Real local process interruption, durable receipts, concurrency and loop suppression."""
from concurrent.futures import ThreadPoolExecutor
from copy import deepcopy
import json
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest
from coordination_checkpoint import Checkpoints


class CheckpointTests(unittest.TestCase):
    def setUp(self):
        temp = tempfile.TemporaryDirectory(); self.addCleanup(temp.cleanup)
        self.root = Path(temp.name); self.path = self.root / 'state.sqlite'
        self.store = Checkpoints(self.path); self.addCleanup(self.store.close)
        self.event = dict(repository='owner/repo', originChangeId='original-change-1',
                          sourceReference='immutable source reference', sourcePlatform='windows',
                          targetPlatform='macos', kind='source-change', visitedPlatforms=['windows'])
        self.delivery = self.store.enqueue(self.event)['id']

    def test_repeated_notification_is_one_delivery(self):
        for _ in range(5):
            self.assertEqual(self.store.enqueue(self.event)['id'], self.delivery)
        self.assertEqual(self.store.db.execute('SELECT count(*) FROM deliveries').fetchone()[0], 1)
        changed = deepcopy(self.event); changed['sourceReference'] = 'different'
        with self.assertRaises(ValueError): self.store.enqueue(changed)

    def test_concurrent_claim_has_single_winner(self):
        def claim(worker):
            store = Checkpoints(self.path)
            try:
                store.claim(self.delivery, worker)
                return True
            except ValueError:
                return False
            finally:
                store.close()
        with ThreadPoolExecutor(max_workers=2) as pool:
            self.assertEqual(sum(pool.map(claim, ['one', 'two'])), 1)

    def crash_worker(self, side_effect):
        module = str(Path(__file__).resolve().parent)
        mailbox = self.root / 'simulated-target-receipt.json'
        code = '''import json, os, sys
from pathlib import Path
sys.path.insert(0, sys.argv[1])
from coordination_checkpoint import Checkpoints
s=Checkpoints(sys.argv[2]); row=s.claim(sys.argv[3], 'crash-fixture', 1, now=1)
if sys.argv[5]=='yes':
    Path(sys.argv[4]).write_text(json.dumps({'deliveryId':row['id'], 'effectRef':'fixture-target:42'}))
os._exit(23)
'''
        result = subprocess.run([sys.executable, '-c', code, module, str(self.path), self.delivery,
                                 str(mailbox), 'yes' if side_effect else 'no'], capture_output=True)
        self.assertEqual(result.returncode, 23)
        return mailbox

    def test_process_crash_before_send_never_blindly_retries(self):
        mailbox = self.crash_worker(False)
        self.assertFalse(mailbox.exists())
        self.assertEqual(self.store.recover(now=3), [self.delivery])
        row = self.store.get(self.delivery)
        with self.assertRaises(ValueError): self.store.claim(self.delivery, 'replacement')
        with self.assertRaises(ValueError): self.store.reconcile_absent(self.delivery, row['token'], 'no target receipt')
        self.store.reconcile_absent(self.delivery, row['token'], 'Exited process + simulated mailbox checked absent',
                                    worker_stopped=True, absence_verified=True)
        new = self.store.claim(self.delivery, 'replacement')
        self.assertNotEqual(new['token'], row['token'])
        with self.assertRaises(ValueError): self.store.confirm(self.delivery, row['token'], 'late:1', 'stale fixture')

    def test_process_crash_after_effect_recovers_without_resend(self):
        mailbox = self.crash_worker(True)
        self.store.recover(now=3)
        row = self.store.get(self.delivery); receipt = json.loads(mailbox.read_text())
        self.assertEqual(receipt['deliveryId'], self.delivery)
        result = self.store.confirm(self.delivery, row['token'], receipt['effectRef'], 'Actual isolated target receipt read')
        self.assertEqual(result['state'], 'confirmed')
        self.assertEqual(self.store.confirm(self.delivery, row['token'], receipt['effectRef'], 'retry')['state'], 'confirmed')
        with self.assertRaises(ValueError): self.store.claim(self.delivery, 'replacement')
        self.assertEqual(self.store.recover(now=100), [])

    def test_alignment_result_does_not_trigger_return_development(self):
        row = self.store.claim(self.delivery, 'fixture')
        self.store.confirm(self.delivery, row['token'], 'fixture-target:42', 'fixture receipt')
        followup = dict(self.event, sourcePlatform='macos', targetPlatform='windows',
                        kind='alignment-result', parentDeliveryId=self.delivery,
                        visitedPlatforms=['windows', 'macos'])
        result = self.store.enqueue(followup)
        self.assertEqual(result['state'], 'suppressed-loop')
        with self.assertRaises(ValueError): self.store.claim(result['id'], 'should-not-run')
        # Genuine independent development gets a new origin ID, not an echo of the old one.
        fresh = dict(self.event, originChangeId='independent-mac-change', sourcePlatform='macos',
                     targetPlatform='windows', visitedPlatforms=['macos'])
        self.assertEqual(self.store.enqueue(fresh)['state'], 'queued')

    def test_missing_or_forged_parent_provenance_blocks(self):
        followup = dict(self.event, sourcePlatform='macos', targetPlatform='windows',
                        kind='alignment-result', parentDeliveryId=self.delivery, visitedPlatforms=['macos'])
        with self.assertRaises(ValueError): self.store.enqueue(followup)
        row = self.store.claim(self.delivery, 'fixture')
        self.store.confirm(self.delivery, row['token'], 'fixture:42', 'fixture')
        with self.assertRaisesRegex(ValueError, 'dropped'): self.store.enqueue(followup)


if __name__ == '__main__':
    unittest.main()
