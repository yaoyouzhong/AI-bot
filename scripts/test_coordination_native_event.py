from copy import deepcopy
import unittest
from coordination_native_event import build_event


class NativeEventTests(unittest.TestCase):
    def setUp(self):
        self.event = dict(id='observation', repo='owner/repo', branch='task-source', before='a'*40, after='b'*40)
        self.route = dict(repository='owner/repo', branch='task-source', sourcePlatform='windows',
                          targetPlatform='macos', mappingVerified=True, mappingEvidence='fixture')
        self.provenance = dict(sourceEventId='observation', sourceCommit='b'*40, originChangeId='origin',
                               kind='source-change', visitedPlatforms=['windows'], verified=True, evidence='fixture')
        self.binding = dict(projectKey='ai-bot', workItemId='task-1', eventType='aibot.native.probe', verified=True, evidence='fixture')

    def build(self):
        return build_event(self.event, self.route, self.provenance, self.binding)

    def test_deterministic_native_shape(self):
        first = self.build()
        self.assertEqual(first, self.build())
        self.assertEqual(set(first), {'id','type','subject','summary','detail','link'})
        self.assertLessEqual(len(first['subject']), 100)
        self.assertLessEqual(len(first['id']), 200)

    def test_new_task_new_lane_same_task_revision_same_lane(self):
        first = self.build()
        self.event.update(id='observation2', before='b'*40, after='c'*40)
        self.provenance.update(sourceEventId='observation2', sourceCommit='c'*40)
        revised = self.build()
        self.assertEqual(first['subject'], revised['subject'])
        self.assertNotEqual(first['id'], revised['id'])
        self.binding['workItemId'] = 'task-2'
        self.assertNotEqual(first['subject'], self.build()['subject'])

    def test_project_isolation(self):
        first = self.build()
        self.binding['projectKey'] = 'other'
        self.assertNotEqual(first['subject'], self.build()['subject'])

    def test_fail_closed_unknown_route_provenance_and_derived_result(self):
        for obj, key, value in [(self.route,'branch','wrong'), (self.provenance,'sourceCommit','d'*40),
                                (self.provenance,'kind','alignment-result'),
                                (self.provenance,'visitedPlatforms',['windows','macos']),
                                (self.binding,'verified',False), (self.binding,'eventType','bad type')]:
            old = deepcopy(obj[key]); obj[key] = value
            with self.assertRaises(ValueError): self.build()
            obj[key] = old

    def test_payload_limit(self):
        self.provenance['originChangeId'] = 'x'*4001
        with self.assertRaises(ValueError): self.build()


if __name__ == '__main__': unittest.main()
