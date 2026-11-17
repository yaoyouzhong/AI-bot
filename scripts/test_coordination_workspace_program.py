import json
from pathlib import Path
import tempfile
import unittest
from coordination_main_review import alignment_event
from coordination_workspace_program import prepare,target_head

class WorkspaceTests(unittest.TestCase):
    def setUp(self):
        self.temp=tempfile.TemporaryDirectory(); self.addCleanup(self.temp.cleanup); self.root=Path(self.temp.name)
        self.binding=dict(repository='owner/repo',targetPlatform='macos',eventType='main.align',verified=True,evidence='fixture',
                          mode='isolated-alignment',access='workspace-write',isolatedWorkspaceVerified=True,
                          workspaceRef='fixed-task-space',autoPush=False,autoMerge=False,autoRelease=False)
        self.config=dict(binding=self.binding,eventSource='local',workspaceRoot=str(self.root/'trees'),isolatedClone=str(self.root/'repo'),
                         isolatedCloneVerified=True,gitExecutable='/fixed/git')
        self.commands=[]
    def context(self,head='a'*40): return dict(events=[dict(alignment_event('owner/repo','main',head,self.binding),source='local')])
    def git(self,args):
        self.commands.append(args)
        if args[0]=='rev-parse': return 'true'
        if args[0]=='remote': return 'https://github.com/owner/repo.git'
        if args[0]=='worktree-head': return Path(args[1]).name
        if args[:2]==['worktree','add']: Path(args[3]).mkdir()
        return ''
    def test_create_and_dirty_resume_preserves_work(self):
        tree=prepare(self.config,self.context(),self.git); (tree/'dirty.txt').write_text('keep')
        self.commands=[]; self.assertEqual(prepare(self.config,self.context(),self.git),tree)
        self.assertEqual((tree/'dirty.txt').read_text(),'keep')
        self.assertFalse(any(x[0] in ('fetch','reset','worktree') for x in self.commands))
    def test_new_head_separate_worktree(self):
        self.assertNotEqual(prepare(self.config,self.context(),self.git),prepare(self.config,self.context('b'*40),self.git))
    def test_untrusted_detail_not_command(self):
        context=self.context(); detail=json.loads(context['events'][0]['detail']); detail['sourceCommit']=';touch /tmp/bad'
        context['events'][0]['detail']=json.dumps(detail)
        with self.assertRaises(ValueError): prepare(self.config,context,self.git)
        self.assertEqual(self.commands,[])
    def test_mixed_head_context_rejected(self):
        context=self.context(); context['events']+=self.context('b'*40)['events']
        with self.assertRaises(ValueError): target_head(context,self.binding,'local')
    def test_lock_not_stolen_and_unmarked_tree_not_reused(self):
        root=Path(self.config['workspaceRoot']); root.mkdir(); (root/'.prepare-lock').mkdir()
        with self.assertRaises(ValueError): prepare(self.config,self.context(),self.git)
        (root/'.prepare-lock').rmdir(); (root/('a'*40)).mkdir()
        with self.assertRaises(ValueError): prepare(self.config,self.context(),self.git)

if __name__=='__main__': unittest.main()
