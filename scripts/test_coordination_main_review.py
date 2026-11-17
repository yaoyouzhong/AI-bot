import unittest
from coordination_main_review import baseline,review_event,alignment_event


class MainReviewTests(unittest.TestCase):
    def setUp(self):
        self.binding=dict(repository='owner/repo',targetPlatform='macos',eventType='main.review',
                          verified=True,evidence='isolated fixture',access='read',mode='review-only')

    def event(self,head='b'*40): return review_event('owner/repo','main',head,self.binding)

    def test_baseline_preserves_backlog_even_equal_is_not_aligned(self):
        result=baseline('owner/repo','b'*40,dict(windows=dict(head='b'*40,relation='equal',evidence='git'),
                                               macos=dict(head='a'*40,relation='behind',evidence='git')),'remote git')
        for item in result['targets'].values():
            self.assertIsNone(item['alignedHead']); self.assertEqual(item['status'],'awaiting-target-review')
        self.assertEqual(result['targets']['macos']['pendingReview'],dict(base='a'*40,head='b'*40))

    def test_stable_same_head_new_head_new_task(self):
        self.assertEqual(self.event(),self.event()); self.assertNotEqual(self.event()['subject'],self.event('c'*40)['subject'])

    def test_target_isolation(self):
        old=self.event(); self.binding['targetPlatform']='windows'
        self.assertNotEqual(old['id'],self.event()['id'])

    def test_other_branch_and_write_mode_rejected(self):
        with self.assertRaises(ValueError): review_event('owner/repo','feature','b'*40,self.binding)
        self.binding['access']='write'
        with self.assertRaises(ValueError): self.event()

    def test_no_source_inference_or_alignment_claim(self):
        import json
        data=json.loads(self.event()['detail']); self.assertIsNone(data['sourcePlatform'])
        self.assertEqual(data['mode'],'review-only')

    def test_isolated_alignment_has_distinct_identity_and_no_external_publish(self):
        old=self.event()
        self.binding.update(mode='isolated-alignment',access='workspace-write',isolatedWorkspaceVerified=True,
                            workspaceRef='task-worktree-1',autoPush=False,autoMerge=False,autoRelease=False)
        new=alignment_event('owner/repo','main','b'*40,self.binding)
        self.assertNotEqual(old['id'],new['id'])
        self.binding['autoPush']=True
        with self.assertRaises(ValueError): alignment_event('owner/repo','main','b'*40,self.binding)

    def test_alignment_refuses_unverified_worktree(self):
        self.binding.update(mode='isolated-alignment',isolatedWorkspaceVerified=False)
        with self.assertRaises(ValueError): alignment_event('owner/repo','main','b'*40,self.binding)

    def test_contradictory_snapshot_rejected(self):
        targets={p:dict(head='b'*40,relation='behind',evidence='git') for p in ('windows','macos')}
        with self.assertRaises(ValueError): baseline('owner/repo','b'*40,targets,'git')


if __name__=='__main__': unittest.main()
