"""No QQ calls; durable unknown state and receipt identity regression tests."""
from pathlib import Path
import sqlite3
import subprocess
import sys
import tempfile
import unittest
from coordination_notifications import Notifications


class NotificationTests(unittest.TestCase):
    def setUp(self):
        temp = tempfile.TemporaryDirectory(); self.addCleanup(temp.cleanup)
        self.path = Path(temp.name) / 'notifications.sqlite'
        self.ledger = Notifications(self.path); self.addCleanup(self.ledger.close)
        self.payload = {'body': 'ISOLATED FIXTURE, NOT SENT'}
        self.ledger.enqueue('task:fixture:done', 'qq', 'fixture-recipient', self.payload)

    def confirm(self, key, token, receipt='fixture-transport:42'):
        return self.ledger.confirm(key, token, receipt, observed_recipient='fixture-recipient',
                                   observed_payload=self.payload, observed_at=self.ledger.get(key)['started_at'], evidence='Isolated simulated transport receipt')

    def test_same_key_cannot_send_twice(self):
        row = self.ledger.begin('task:fixture:done')
        self.confirm(row['key'], row['token'])
        self.assertEqual(self.ledger.enqueue(row['key'], 'qq', 'fixture-recipient', self.payload)['state'], 'sent')
        with self.assertRaises(ValueError): self.ledger.begin(row['key'])

    def test_process_interruption_requires_reconciliation(self):
        code = '''import os,sys
sys.path.insert(0,sys.argv[1])
from coordination_notifications import Notifications
n=Notifications(sys.argv[2]); n.begin('task:fixture:done'); os._exit(29)
'''
        result = subprocess.run([sys.executable, '-c', code, str(Path(__file__).resolve().parent), str(self.path)])
        self.assertEqual(result.returncode, 29)
        self.ledger.recover()
        row = self.ledger.get('task:fixture:done')
        self.assertEqual(row['state'], 'unknown')
        with self.assertRaises(ValueError): self.ledger.begin(row['key'])
        self.assertEqual(self.confirm(row['key'], row['token'])['state'], 'sent')

    def test_old_receipt_not_reusable_for_new_key(self):
        first = self.ledger.begin('task:fixture:done'); self.confirm(first['key'], first['token'])
        self.ledger.enqueue('task:another:done', 'qq', 'fixture-recipient', self.payload)
        second = self.ledger.begin('task:another:done')
        with self.assertRaises(sqlite3.IntegrityError): self.confirm(second['key'], second['token'])
        self.assertEqual(self.ledger.get(second['key'])['state'], 'sending')

    def test_wrong_message_or_recipient_cannot_confirm(self):
        row = self.ledger.begin('task:fixture:done')
        for recipient, payload in [('other', self.payload), ('fixture-recipient', {'body': 'different'})]:
            with self.assertRaises(ValueError):
                self.ledger.confirm(row['key'], row['token'], 'fixture:42', observed_recipient=recipient,
                                    observed_payload=payload, observed_at=row['started_at'], evidence='not matching')
        self.assertEqual(self.ledger.get(row['key'])['state'], 'sending')

    def test_historical_receipt_time_cannot_confirm_new_attempt(self):
        row = self.ledger.begin('task:fixture:done')
        with self.assertRaisesRegex(ValueError, 'predates'):
            self.ledger.confirm(row['key'], row['token'], 'old:42', observed_recipient='fixture-recipient',
                                observed_payload=self.payload, observed_at=row['started_at'] - 100, evidence='old receipt')

    def test_same_key_different_body_rejected(self):
        with self.assertRaises(ValueError):
            self.ledger.enqueue('task:fixture:done', 'qq', 'fixture-recipient', {'body': 'changed'})


if __name__ == '__main__':
    unittest.main()
