import hashlib
import unittest
from copy import deepcopy
from coordination_checkpoint import canonical
from coordination_native_mailbox import NativeMailbox
import test_coordination_native_event as native_fixture


class NativeMailboxTests(unittest.TestCase):
    def setUp(self):
        fixture = native_fixture.NativeEventTests(); fixture.setUp()
        self.route, self.binding = fixture.route, fixture.binding
        self.binding['expectedRule'] = 'native-probe'
        self.packet = dict(schemaVersion=1, mode='isolated-test', event=fixture.event,
                           provenance=fixture.provenance, sourcePlatform='windows', targetPlatform='macos')
        identity = hashlib.sha256(canonical(self.packet).encode()).hexdigest()
        self.packet['packetId'] = identity; self.binding['expectedPacketId'] = identity
        self.box = NativeMailbox(dict(mode='isolated-test',enabled=True, repository='owner/repo', branch='coord-test',
                                      namespace='isolated-test/native',writeAuthorized=True,authorizationEvidence='fixture',ghExecutable='/fixed/gh'))
        self.box.runner = lambda args: {'object': {'sha':'f'*40}}
        self.box.get_file = lambda path, ref: deepcopy(self.packet)
        self.receipts = {}; self.keys = {}; self.calls = 0; self.lose = False; self.state = 'claimed'
        self.box.create = self.create

    def create(self, filename, content):
        if filename in self.receipts and self.receipts[filename] != content: raise ValueError('conflict')
        existed = filename in self.receipts; self.receipts[filename] = content
        return dict(existed=existed)

    def hub(self, method, path, payload=None):
        self.calls += 1
        if method == 'POST':
            self.keys.setdefault(payload['id'], len(self.keys)+1)
            if self.lose: self.lose = False; raise TimeoutError('reply lost after acceptance')
            return dict(event=self.keys[payload['id']], routed=['native-probe'])
        return dict(event=1,routed=['native-probe'],runs=[dict(run=9,rule='native-probe',state=self.state)])

    def poll(self): return self.box.poll_native(self.route,self.binding,self.hub)

    def test_running_then_terminal_repeat_no_new_native_event(self):
        self.assertEqual(self.poll()['state'],'claimed'); self.assertFalse(self.receipts)
        self.state = 'done'; self.assertFalse(self.poll()['receipt']['existed'])
        self.assertTrue(self.poll()['receipt']['existed']); self.assertEqual(len(self.keys),1)

    def test_response_loss_repeat_same_identity(self):
        self.lose = True
        with self.assertRaises(TimeoutError): self.poll()
        self.state = 'done'; self.poll(); self.assertEqual(len(self.keys),1)

    def test_packet_corruption_never_calls_hub(self):
        self.packet['event']['after'] = 'c'*40
        with self.assertRaises(ValueError): self.poll()
        self.assertEqual(self.calls,0)

    def test_missing_packet_is_not_failure_or_success(self):
        self.box.get_file = lambda path,ref: None
        self.assertEqual(self.poll()['state'],'awaiting-packet'); self.assertEqual(self.calls,0)

    def test_multiple_runs_no_receipt(self):
        def hub(method,path,payload=None):
            if method=='POST': return dict(event=1,routed=['native-probe'])
            return dict(event=1,routed=['native-probe'],runs=[dict(run=i,rule='native-probe',state='done') for i in (1,2)])
        with self.assertRaises(ValueError): self.box.poll_native(self.route,self.binding,hub)
        self.assertFalse(self.receipts)

    def test_conflicting_receipt_never_overwrites(self):
        key = self.binding['expectedPacketId']+'.receipt.json'; self.receipts[key] = {'old':'evidence'}
        self.state='done'
        with self.assertRaises(ValueError): self.poll()
        self.assertEqual(self.receipts[key],{'old':'evidence'})

    def test_paused_route_not_confirmed(self):
        def hub(method,path,payload=None):
            return dict(event=1,routed=['native-probe'],paused=['native-probe'])
        with self.assertRaises(ValueError): self.box.poll_native(self.route,self.binding,hub)
        self.assertFalse(self.receipts)

    def test_read_outage_no_native_send(self):
        def fail(path,ref): raise TimeoutError('github offline')
        self.box.get_file=fail
        with self.assertRaises(TimeoutError): self.poll()
        self.assertEqual(self.calls,0)


if __name__=='__main__': unittest.main()
