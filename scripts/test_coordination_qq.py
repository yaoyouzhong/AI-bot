"""Offline fake QQ only. No user messages or credentials."""
from pathlib import Path
import sys
import tempfile
import unittest
from coordination_checkpoint import Checkpoints
from coordination_notifications import Notifications
from coordination_qq import QQNotifier


class QQTests(unittest.TestCase):
    def setUp(self):
        temp = tempfile.TemporaryDirectory(); self.addCleanup(temp.cleanup)
        self.checkpoints = Checkpoints(Path(temp.name)/'checkpoint.db'); self.addCleanup(self.checkpoints.close)
        self.notifications = Notifications(Path(temp.name)/'notification.db'); self.addCleanup(self.notifications.close)
        event = dict(repository='owner/repo', originChangeId='fixture', sourceReference='fixture',
                     sourcePlatform='windows', targetPlatform='macos', kind='source-change', visitedPlatforms=['windows'])
        row = self.checkpoints.enqueue(event); claim = self.checkpoints.claim(row['id'], 'fixture')
        self.delivery = row['id']
        self.checkpoints.confirm(self.delivery, claim['token'], 'fixture-run:17', 'fixture evidence')
        self.checkpoints.observe_run(self.delivery, 'fixture-run:17', 'done', 'fixture evidence')
        self.binding = dict(command=[sys.executable, 'unused-fixture'], recipient='fixture-only',
                            bindingVerified=True, bindingEvidence='offline fixture', sendAuthorized=True)
        self.calls = 0; self.output = 'qq:fixture-42'; self.fail = False
        self.notifier = QQNotifier(self.binding, self.notifications, self.checkpoints, self.invoke)

    def invoke(self, body):
        self.calls += 1
        self.assertIn('不代表代码对齐完成', body)
        if self.fail: raise TimeoutError('fake accepted then response lost')
        return self.output

    def test_acceptance_not_delivery_and_duplicate_suppressed(self):
        first = self.notifier.notify_execution(self.delivery)
        second = self.notifier.notify_execution(self.delivery)
        self.assertTrue(first['providerAccepted']); self.assertFalse(first['deliveryConfirmed'])
        self.assertFalse(first['readConfirmed']); self.assertFalse(second['sentNow']); self.assertEqual(self.calls, 1)

    def test_opaque_provider_id_with_exclamation_is_accepted(self):
        self.output = 'qq:opaque_provider.fixture!!'
        result = self.notifier.notify_execution(self.delivery)
        self.assertTrue(result['providerAccepted'])
        self.assertEqual(result['receipt'], self.output)

    def test_unknown_never_blindly_resends(self):
        self.fail = True
        with self.assertRaises(TimeoutError): self.notifier.notify_execution(self.delivery)
        self.fail = False
        result = self.notifier.notify_execution(self.delivery)
        self.assertEqual(result['state'], 'unknown'); self.assertFalse(result['providerAccepted'])
        self.assertEqual(self.calls, 1)

    def test_unrecognized_response_stays_unknown(self):
        self.output = 'Accepted'
        with self.assertRaises(ValueError): self.notifier.notify_execution(self.delivery)
        self.assertEqual(self.notifier.notify_execution(self.delivery)['state'], 'unknown')
        self.assertEqual(self.calls, 1)

    def test_no_send_authorization_means_no_transport_call(self):
        notifier = QQNotifier(dict(self.binding, sendAuthorized=False), self.notifications, self.checkpoints, self.invoke)
        self.assertEqual(notifier.notify_execution(self.delivery)['state'], 'awaiting-authorization')
        self.assertEqual(self.calls, 0)


if __name__ == '__main__': unittest.main()
