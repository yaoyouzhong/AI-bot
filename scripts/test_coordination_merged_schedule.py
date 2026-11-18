from copy import deepcopy
import json
import unittest
from coordination_checkpoint import canonical
from coordination_main_eligibility import schedule_proof, validate_schedule_proof, evidence
from coordination_main_watch import watch_once
import test_coordination_main_eligibility as fixtures


def tick(minute, **patch):
    return dict(dict(id='fixed-watch@2026-11-18T01:'+minute+':00.000Z',source='schedule',type='schedule.tick',
        hops=0,receivedAt='2026-11-18T01:'+minute+':00.001Z'),**patch)


class ScheduleTests(unittest.TestCase):
    def test_merged_retry_stable_order_and_dedup(self):
        events=[tick('10'),tick('00'),tick('05'),tick('05')]
        proof=schedule_proof(dict(events=events),'123','fixed-watch')
        self.assertEqual(proof,schedule_proof(dict(events=list(reversed(events))),'123','fixed-watch'))
        self.assertEqual(len(proof['scheduleEvents']),3);self.assertEqual(proof['receivedAt'],tick('10')['receivedAt'])
        self.assertEqual(validate_schedule_proof(proof,'fixed-watch'),proof)

    def test_single_legacy_shape_unchanged(self):
        proof=schedule_proof(dict(events=[tick('00'),tick('00')]),'123','fixed-watch')
        self.assertNotIn('scheduleProofVersion',proof)
        self.assertEqual(validate_schedule_proof(proof,'fixed-watch'),proof)

    def test_mixed_or_wrong_rule_event_refused_in_every_position(self):
        for bad in [tick('05',source='local'),tick('05',type='manual'),tick('05',hops=1),
                    tick('05',hops=False),tick('05',id='other-watch@2026-11-18T01:05:00.000Z'),None]:
            for events in ([tick('00'),bad],[bad,tick('00')]):
                with self.assertRaises(ValueError):schedule_proof(dict(events=events),'123','fixed-watch')

    def test_conflicting_duplicate_times_or_invalid_time_refused(self):
        for bad in [tick('00',receivedAt='2026-11-18T01:00:01Z'),tick('05',receivedAt='private /Users/path'),
                    tick('05',id='fixed-watch@2026-99-18T01:05:00.000Z')]:
            with self.assertRaises(ValueError):schedule_proof(dict(events=[tick('00'),bad]),'123','fixed-watch')

    def test_empty_overflow_and_answer_context_refused(self):
        for events in ([],[tick('00')]*1001):
            with self.assertRaises(ValueError):schedule_proof(dict(events=events),'123','fixed-watch')
        with self.assertRaises(ValueError):schedule_proof(dict(events=[tick('00')],answers=[{}]),'123','fixed-watch')

    def test_stored_bundle_tamper_or_unknown_version_refused(self):
        proof=schedule_proof(dict(events=[tick('00'),tick('05')]),'123','fixed-watch')
        for patch in ({'receivedAt':tick('00')['receivedAt']},{'scheduleProofVersion':3},{'private':'session receipt'}):
            with self.assertRaises(ValueError):validate_schedule_proof(dict(proof,**patch),'fixed-watch')
        proof['scheduleEvents'].reverse()
        with self.assertRaises(ValueError):validate_schedule_proof(proof,'fixed-watch')


class MergedFlowTests(fixtures.FutureTests):
    def setUp(self):
        super().setUp()
        self.proof=schedule_proof(dict(events=[tick('00'),tick('05')]),'123','fixed-watch')
        self.path.write_text(canonical(evidence(self.binding,self.head,self.observation['event'],self.proof)))

    def test_merged_qualifies_after_outage_once_without_private_export(self):
        original=self.github
        def offline(endpoint):raise TimeoutError()
        self.github=offline
        with self.assertRaises(TimeoutError):self.poll()
        self.assertEqual(self.writes,0)
        self.github=original;self.poll();self.poll();self.assertEqual(self.writes,1)
        value=canonical(next(iter(self.files.values())))
        for text in ('scheduleId','watcherRun','scheduleEvents','fixed-watch','private','session'):
            self.assertNotIn(text,value)

    def test_retry_merged_events_keeps_one_task_and_original_proof(self):
        cfg=dict(self.watch,dispatchEnabled=True,baselineVerified=True,workspaceDispatcherVerified=True)
        keys={}
        def hub(method,path,payload=None):
            if method=='POST':
                keys.setdefault(payload['id'],len(keys)+1);return dict(event=keys[payload['id']],routed=['fixed-rule'])
            return dict(event=int(path.split('/')[-1]),routed=['fixed-rule'],runs=[])
        first=schedule_proof(dict(events=[tick('00')]),'123','fixed-watch')
        watch_once(cfg,lambda r:dict(sha='f'*40),hub,first)
        before=json.loads((self.root/'main-eligibility'/('f'*40+'.json')).read_text())
        watch_once(cfg,lambda r:dict(sha='f'*40),hub,self.proof)
        after=json.loads((self.root/'main-eligibility'/('f'*40+'.json')).read_text())
        self.assertEqual(before,after);self.assertEqual(len(keys),2) # existing b + new f

    def test_merged_native_acceptance_reply_loss_reuses_event_identity(self):
        cfg=dict(self.watch,dispatchEnabled=True,baselineVerified=True,workspaceDispatcherVerified=True)
        keys={};lose=True
        def hub(method,path,payload=None):
            nonlocal lose
            if method=='POST':
                event=keys.setdefault(payload['id'],len(keys)+1)
                if lose:lose=False;raise TimeoutError('accepted but reply lost')
                return dict(event=event,routed=['fixed-rule'])
            return dict(event=int(path.split('/')[-1]),routed=['fixed-rule'],runs=[])
        with self.assertRaises(TimeoutError):watch_once(cfg,lambda r:dict(sha='f'*40),hub,self.proof)
        watch_once(cfg,lambda r:dict(sha='f'*40),hub,self.proof)
        watch_once(cfg,lambda r:dict(sha='f'*40),hub,self.proof)
        self.assertEqual(len(keys),2)
        saved=json.loads((self.root/'main-eligibility'/('f'*40+'.json')).read_text())
        self.assertEqual(saved['proof'],self.proof)

    def test_merged_ancestry_and_nonterminal_still_fail_closed(self):
        original=self.github
        def diverged(endpoint):
            x=original(endpoint)
            if endpoint.startswith('compare/'):x['status']='diverged'
            return x
        self.github=diverged
        with self.assertRaises(ValueError):self.poll()
        self.github=original;self.native['runs'][0]['state']='unknown'
        self.assertEqual(self.poll()['results'][0]['state'],'not-terminal');self.assertEqual(self.writes,0)

for name in dir(fixtures.FutureTests):
    if name.startswith('test_'):setattr(MergedFlowTests,name,None)

if __name__=='__main__':unittest.main()
