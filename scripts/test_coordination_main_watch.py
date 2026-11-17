import json
from pathlib import Path
import tempfile
import unittest
from coordination_main_watch import watch_once

class WatchTests(unittest.TestCase):
    def setUp(self):
        self.temp=tempfile.TemporaryDirectory(); self.addCleanup(self.temp.cleanup)
        self.binding=dict(repository='owner/repo',targetPlatform='macos',eventType='main.review',verified=True,
                          evidence='fixture',access='read',mode='review-only')
        self.config=dict(enabled=True,branch='main',binding=self.binding,expectedRule='probe',baselineVerified=True,auditRoot=self.temp.name)
        self.head='a'*40; self.keys={}; self.lose=False
    def hub(self,method,path,payload=None):
        if method=='POST':
            value=self.keys.setdefault(payload['id'],len(self.keys)+1)
            if self.lose: self.lose=False; raise TimeoutError('accepted response lost')
            return dict(event=value,routed=['probe'])
        return dict(event=int(path.split('/')[-1]),routed=['probe'],runs=[])
    def tick(self): return watch_once(self.config,lambda repo:dict(sha=self.head),self.hub)
    def test_restart_response_loss_dedup_and_old_observations_retained(self):
        self.lose=True
        with self.assertRaises(TimeoutError): self.tick()
        self.tick(); self.assertEqual(len(self.keys),1)
        self.head='b'*40; result=self.tick(); self.assertEqual(len(self.keys),2)
        self.assertEqual(len(result['results']),2)
        self.assertTrue(all(x['alignedHead'] is None for x in result['results']))
    def test_github_failure_no_event_no_overwrite(self):
        self.tick()
        def fail(repo): raise TimeoutError()
        with self.assertRaises(TimeoutError): watch_once(self.config,fail,self.hub)
        self.assertEqual(len(self.keys),1)
    def test_tampered_observation_no_dispatch(self):
        self.tick(); path=Path(self.temp.name)/'observations'/('a'*40+'.json')
        path.write_text('{}')
        with self.assertRaises(ValueError): self.tick()
    def test_unverified_dynamic_workspace_refused(self):
        self.binding.update(mode='isolated-alignment',access='workspace-write')
        with self.assertRaises(ValueError): self.tick()
    def test_pause_not_reported_as_execution(self):
        def paused(method,path,payload=None): return dict(event=1,routed=[],paused=['probe'],runs=[])
        result=watch_once(self.config,lambda repo:dict(sha=self.head),paused)
        self.assertEqual(result['results'][0]['state'],'observed-not-routed')

if __name__=='__main__': unittest.main()
