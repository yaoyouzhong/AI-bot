from copy import deepcopy
import hashlib
import json
from pathlib import Path
import unittest
from coordination_checkpoint import canonical
from coordination_main_eligibility import binding_digest, schedule_proof, evidence
from coordination_main_watch import watch_once
from coordination_result_writeback import writeback_once
import test_coordination_result_writeback as fixtures


class FutureTests(fixtures.ResultTests):
    def setUp(self):
        super().setUp()
        self.config['nativeSource']='local'
        self.config['futureMain']=dict(enabled=True,anchorCommit='a'*40,enabledAt='2026-11-18T00:00:00Z',
            bindingDigest=binding_digest(self.binding),watcherRule='fixed-watch',expectedRule='fixed-rule',nativeSource='local')
        self.config['authorizedSourceCommits']=['d'*40];self.config['publicFacts']={}
        self.watch['resultEligibility']=dict(enabled=True,bindingDigest=binding_digest(self.binding),watcherRule='fixed-watch')
        self.proof=schedule_proof(dict(events=[dict(source='schedule',type='schedule.tick',hops=0,
            id='fixed-watch@2026-11-18T01:00:00.000Z',receivedAt='2026-11-18T01:00:00.001Z')]),'123','fixed-watch')
        self.path=self.root/'main-eligibility'/(self.head+'.json');self.path.parent.mkdir()
        self.path.write_text(canonical(evidence(self.binding,self.head,self.observation['event'],self.proof)))
        self.row=dict(head=self.head,nativeEvent=25,nativeRun=35)
        (self.root/'native-observations').mkdir();self.audit()

    def audit(self):
        digest=hashlib.sha256(canonical(self.row).encode()).hexdigest()
        (self.root/'native-observations'/(digest+'.json')).write_text(canonical(self.row))

    def github(self,endpoint):
        if endpoint=='commits/main':return dict(sha='c'*40)
        base,head=endpoint.split('/')[1].split('...')
        return dict(base_commit=dict(sha=base),merge_base_commit=dict(sha=base),status='ahead')

    def poll(self):return writeback_once(self.config,self.watch,self.hub,self.box,self.github)

    # Run only future-specific cases; legacy tests are independently run in ResultTests.
    def test_future_watcher_partial_observation_retries_without_replacing_proof(self):
        import coordination_main_watch as watch_module
        from unittest.mock import patch
        cfg=dict(self.watch,dispatchEnabled=True,baselineVerified=True,workspaceDispatcherVerified=True)
        original=watch_module.immutable
        def interrupted(path,value):
            if path.parent.name=='observations':raise TimeoutError('interrupted after provenance')
            original(path,value)
        def hub(method,path,payload=None):
            if method=='POST':return dict(event=25,routed=['fixed-rule'])
            return dict(event=25,routed=['fixed-rule'],runs=[])
        with patch.object(watch_module,'immutable',interrupted):
            with self.assertRaises(TimeoutError):watch_once(cfg,lambda r:dict(sha='f'*40),hub,self.proof)
        later=dict(self.proof,watcherRun=124,scheduleId='fixed-watch@2026-11-18T01:05:00.000Z')
        watch_once(cfg,lambda r:dict(sha='f'*40),hub,later)
        saved=json.loads((self.root/'main-eligibility'/('f'*40+'.json')).read_text())
        self.assertEqual(saved['proof'],self.proof)

    def test_future_chain_and_duplicate(self):
        self.poll();self.poll();self.assertEqual(self.writes,1)
        value=next(iter(self.files.values()));self.assertFalse(value['productAccepted'])
        self.assertEqual(value['facts']['decision'],'not-asserted')
        self.assertNotIn('watcherRun',canonical(value));self.assertNotIn('scheduleId',canonical(value))

    def test_future_missing_proof_skips_without_hub_or_write(self):
        self.path.unlink();self.assertEqual(self.poll()['results'][0]['state'],'ineligible-no-natural-proof');self.assertFalse(self.files)

    def test_future_wrong_ancestry_rejects(self):
        original=self.github
        def wrong(endpoint):
            value=original(endpoint)
            if endpoint.startswith('compare/'):value['merge_base_commit']['sha']='e'*40
            return value
        self.github=wrong
        with self.assertRaises(ValueError):self.poll()
        self.assertFalse(self.files)

    def test_future_main_outage_no_write(self):
        def offline(endpoint):raise TimeoutError()
        self.github=offline
        with self.assertRaises(TimeoutError):self.poll()
        self.assertFalse(self.files)

    def test_future_policy_binding_changed_rejects(self):
        self.config['futureMain']['expectedRule']='different'
        with self.assertRaises(ValueError):self.poll()

    def test_future_pre_enable_proof_rejects(self):
        self.config['futureMain']['enabledAt']='2026-11-18T02:00:00Z'
        with self.assertRaises(ValueError):self.poll()

    def test_future_missing_native_audit_rejects(self):
        for p in (self.root/'native-observations').glob('*'):p.unlink()
        with self.assertRaises(FileNotFoundError):self.poll()
        self.assertFalse(self.files)

    def test_future_schedule_proof_manual_source_rejects(self):
        for event in [dict(source='local',type='schedule.tick',hops=0,id=self.proof['scheduleId'],receivedAt=self.proof['receivedAt']),
                      dict(source='schedule',type='schedule.tick',hops=1,id=self.proof['scheduleId'],receivedAt=self.proof['receivedAt'])]:
            with self.assertRaises(ValueError):schedule_proof(dict(events=[event]),'1','fixed-watch')

    def test_future_watcher_requires_natural_context_and_distinct_source_identity(self):
        cfg=dict(self.watch,dispatchEnabled=True,baselineVerified=True,workspaceDispatcherVerified=True)
        with self.assertRaises(ValueError):watch_once(cfg,lambda r:dict(sha='f'*40),None)
        def hub(method,path,payload=None):
            if method=='POST':return dict(event=25,routed=['fixed-rule'])
            return dict(event=25,routed=['fixed-rule'],runs=[])
        watch_once(cfg,lambda r:dict(sha='f'*40),hub,self.proof)
        saved=json.loads((self.root/'main-eligibility'/('f'*40+'.json')).read_text())
        self.assertNotEqual(saved['eventKey'],self.observation['event']['id'])

# Inherited fixture tests belong to ResultTests and use legacy configuration.
for name in list(vars(fixtures.ResultTests)):
    if name.startswith('test_'):setattr(FutureTests,name,None)

if __name__=='__main__':unittest.main()
