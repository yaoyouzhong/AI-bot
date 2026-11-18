import base64
from copy import deepcopy
import json
from pathlib import Path
import tempfile
import unittest
from coordination_main_review import alignment_event
from coordination_result_writeback import ResultMailbox, receipt, writeback_once, validate_config, REPOSITORY, BRANCH


class ResultTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory(); self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name); self.head = 'b'*40
        self.config = dict(enabled=True, mode='native-terminal-result', repository=REPOSITORY, branch=BRANCH,
            writeAuthorized=True, authorizationEvidence='fixture', ghExecutable='/fixed/gh',
            authorizedSourceCommits=[self.head], activatedAt='2026-11-18T02:00:00Z',
            publicFacts={self.head:dict(decision='no-code-change',changes='none',checks={'syntax':'passed',
                'workspace':'passed','targeted-runtime':'sandbox-blocked'},gaps=['targeted-runtime-sandbox-blocked'])},
            watchSnapshot=str(self.root/'snapshot.json'))
        self.binding = dict(repository=REPOSITORY,targetPlatform='macos', mode='isolated-alignment',access='workspace-write',
            verified=True,evidence='fixture',eventType='main.macos',isolatedWorkspaceVerified=True,
            workspaceRef='/private/local/path',autoPush=False,autoMerge=False,autoRelease=False)
        self.watch = dict(enabled=True,branch='main',binding=self.binding,expectedRule='fixed-rule',auditRoot=str(self.root))
        self.observation = dict(repository=REPOSITORY,branch='main',head=self.head,
            event=alignment_event(REPOSITORY,'main',self.head,self.binding))
        (self.root/'observations').mkdir(); self.save()
        self.native = dict(event=25,routed=['fixed-rule'],paused=[],runs=[dict(run=35,rule='fixed-rule',state='nothing',
            finishedAt='2026-11-18T01:51:35.503Z',summary='PRIVATE conversation /Users/person token receipt',events=[{'secret':'private'}])])
        self.files = {}; self.writes = 0; self.requests = []; self.loss = False; self.race = False; self.offline = False
        self.box = ResultMailbox(self.config,self.api)

    def save(self):
        (self.root/'snapshot.json').write_text(json.dumps(dict(results=[dict(head=self.head,nativeEvent=25,nativeRun=35)])))
        (self.root/'observations'/(self.head+'.json')).write_text(json.dumps(self.observation))

    def api(self,args,payload=None):
        self.requests.append((args,payload))
        if self.offline: raise TimeoutError('unreadable')
        path = args[0].split('/contents/')[1].split('?')[0]
        if payload is not None:
            self.assertEqual(payload['branch'],BRANCH); self.assertNotIn('sha',payload)
            if self.race: self.race = False; raise ValueError('409 branch CAS race before commit')
            if path in self.files: raise ValueError('409 existing blob')
            self.files[path] = json.loads(base64.b64decode(payload['content'])); self.writes += 1
            if self.loss: self.loss=False; raise TimeoutError('committed but response lost')
            return {'commit':{'sha':'c'*40}}
        if path in self.files: return dict(encoding='base64',content=base64.b64encode(json.dumps(self.files[path]).encode()).decode())
        return None

    def hub(self,method,path):
        self.assertEqual(method,'GET'); self.assertEqual(path,'/v1/events/25'); return deepcopy(self.native)

    def poll(self): return writeback_once(self.config,self.watch,self.hub,self.box)

    def test_real_shape_terminal_repeat_and_privacy(self):
        self.assertFalse(self.poll()['results'][0]['existed']); self.assertTrue(self.poll()['results'][0]['existed'])
        self.assertEqual(self.writes,1); value=next(iter(self.files.values()))
        self.assertFalse(value['productAccepted']);self.assertFalse(value['publicationPerformed'])
        self.assertEqual(value['provenance']['observation'],'backfill')
        self.assertEqual(value['facts']['checks']['targeted-runtime'],'sandbox-blocked')
        for text in ('PRIVATE','/Users/','/private/','token','receipt','summary','fixed-rule'):
            self.assertNotIn(text,json.dumps(value))

    def test_execution_result_cannot_satisfy_existing_release_receipt(self):
        import cross_platform_coordination as contract
        task=contract.new_task(REPOSITORY,'main','a'*40,self.head,'fixture only')
        value=receipt(self.config,self.watch,self.observation,self.native)
        with self.assertRaises(ValueError):contract.validate_receipt(task,value)

    def test_same_identity_racer_committed_first(self):
        original=self.box.runner
        raced=False
        def concurrent(args,payload=None):
            nonlocal raced
            if payload is not None and not raced:
                raced=True
                original(args,payload)
                raise ValueError('409 same identity racer won')
            return original(args,payload)
        self.box.runner=concurrent
        self.assertTrue(self.poll()['results'][0]['reconciled'])
        self.poll();self.assertEqual(self.writes,1)

    def test_lost_reply_and_unavailable_read_preserves_unknown_then_reconciles(self):
        original=self.box.runner; lost=False
        def unavailable(args,payload=None):
            nonlocal lost
            if lost:raise TimeoutError('read unavailable; outcome unknown')
            result=original(args,payload)
            if payload is not None:
                lost=True;raise TimeoutError('reply lost')
            return result
        self.box.runner=unavailable
        with self.assertRaises(TimeoutError):self.poll()
        self.box.runner=original;self.poll();self.assertEqual(self.writes,1)

    def test_nonterminal_never_writes(self):
        for state in ('claimed','unknown','queued'):
            self.native['runs'][0]['state']=state;self.assertEqual(self.poll()['results'][0]['state'],'not-terminal')
        self.assertEqual(self.writes,0)

    def test_lost_reply_reconciles_no_second_commit(self):
        self.loss=True;self.assertTrue(self.poll()['results'][0]['reconciled']);self.poll();self.assertEqual(self.writes,1)

    def test_ref_race_rereads_before_bounded_retry(self):
        self.race=True;self.poll();self.assertEqual(self.writes,1)
        self.assertEqual(sum(payload is not None for _,payload in self.requests),2)

    def test_same_path_conflict_never_overwritten(self):
        self.poll();path=next(iter(self.files));self.files[path]={'existing':'different evidence'}
        with self.assertRaises(ValueError):self.poll()
        self.assertEqual(self.writes,1);self.assertEqual(self.files[path],{'existing':'different evidence'})

    def test_other_platform_preserves_first_platform(self):
        self.poll();first=deepcopy(self.files)
        self.binding['targetPlatform']='windows'
        self.observation['event']=alignment_event(REPOSITORY,'main',self.head,self.binding);self.save();self.poll()
        self.assertEqual(len(self.files),2)
        for path,value in first.items():self.assertEqual(self.files[path],value)
        self.assertEqual(len({v['resultId'] for v in self.files.values()}),2)

    def test_unknown_facts_and_free_text_rejected(self):
        for field,value in [('session','private'),('decision','/Users/private')]:
            bad=deepcopy(self.config);bad['publicFacts'][self.head][field]=value
            with self.assertRaises(ValueError):receipt(bad,self.watch,self.observation,self.native)
        self.assertFalse(self.files)

    def test_native_mismatch_and_multiple_runs_rejected(self):
        self.native['event']=26
        with self.assertRaises(ValueError):self.poll()
        self.native['event']=25;self.native['runs'][0]['run']=36
        with self.assertRaises(ValueError):self.poll()
        self.native['runs']*=2
        with self.assertRaises(ValueError):self.poll()
        self.assertFalse(self.files)

    def test_binding_tamper_rejected(self):
        self.observation['event']['detail']='tampered';self.save()
        with self.assertRaises(ValueError):self.poll()
        self.assertFalse(self.files)

    def test_scope_is_hard_bounded(self):
        for patch in ({'branch':'main'},{'repository':'other/repo'},{'enabled':False},{'authorizedSourceCommits':[]}):
            with self.assertRaises(ValueError):validate_config(dict(self.config,**patch))
        self.config['authorizedSourceCommits']=['a'*40];self.config['publicFacts']={}
        self.assertEqual(self.poll()['results'],[]);self.assertFalse(self.files)

    def test_no_facts_no_acceptance_and_live(self):
        self.config['publicFacts']={};self.config['activatedAt']='2026-11-18T00:00:00Z'
        self.poll();value=next(iter(self.files.values()))
        self.assertEqual(value['facts']['decision'],'not-asserted');self.assertFalse(value['productAccepted'])
        self.assertEqual(value['provenance']['observation'],'live')

    def test_cancelled_not_success(self):
        self.native['runs'][0]['state']='cancelled'
        with self.assertRaises(ValueError):self.poll()
        self.config['publicFacts']={};self.poll();self.assertFalse(next(iter(self.files.values()))['productAccepted'])

    def test_outage_or_invalid_time_does_not_write(self):
        self.offline=True
        with self.assertRaises(TimeoutError):self.poll()
        self.offline=False;self.native['runs'][0]['finishedAt']='local private data'
        with self.assertRaises(ValueError):self.poll()
        self.assertFalse(self.files)


if __name__=='__main__':unittest.main()
