"""Exercise product-release orchestration without GitHub mutations."""
from copy import deepcopy
from pathlib import Path
import tempfile
from types import SimpleNamespace
import unittest
from unittest.mock import patch

import product_promotion as promotion
from product_release import plan_identity, render_product
from test_product_release import component
import test_release_pipeline as fixtures


class ProductPromotionTests(unittest.TestCase):
    def setUp(self):
        self.bridge = component('bridge', '0.6.20', 'bridge-v0.6.20', 'b', ['AI-bot-bridge-v0.6.20-macos-arm64.zip'])
        self.tab5 = component('tab5', '0.2.321-ui', 'tab5-v0.2.321-ui', 'a', ['AI-bot-tab5-0.2.321-ui-upgrade.zip'])

    def test_preflight_rejects_missing_acceptance_before_any_mutation(self):
        with patch.object(promotion, 'component_plan', side_effect=ValueError('Pending acceptance')), patch.object(promotion, 'reserve_product') as reserve:
            with self.assertRaisesRegex(ValueError, 'Pending acceptance'):
                promotion.promote_product([{'directory':'fixture'}], 'owner/repo', execute=True, publish=True)
            reserve.assert_not_called()

    def test_legacy_frozen_bridge_candidate_uses_component_tag_without_resealing(self):
        fixture = fixtures.CandidateTests(methodName='runTest'); fixture.setUp(); self.addCleanup(fixture.doCleanups)
        folder, candidate = fixture.candidate()
        legacy = dict(candidate); legacy['tag'] = 'v' + legacy['version']
        legacy['candidateId'] = promotion.pipeline.canonical_id({k:v for k,v in legacy.items() if k != 'candidateId'})
        promotion.pipeline.write(folder / 'CANDIDATE.json', legacy)
        acceptance = fixture.accepted(folder, legacy)
        plan = promotion.component_plan({'directory':folder, 'acceptance':acceptance})
        self.assertEqual(plan['candidate']['tag'], 'bridge-v' + legacy['version'])
        self.assertEqual(plan['candidate']['candidateId'], legacy['candidateId'])
        self.assertEqual(promotion.pipeline.read(folder / 'CANDIDATE.json'), legacy)

    def test_product_assets_reuse_exact_accepted_bytes_and_checksums(self):
        fixture = fixtures.CandidateTests(methodName='runTest'); fixture.setUp(); self.addCleanup(fixture.doCleanups)
        folder, candidate = fixture.candidate()
        acceptance = fixture.accepted(folder, candidate)
        plan = promotion.component_plan({'directory':folder, 'acceptance':acceptance})
        with tempfile.TemporaryDirectory() as temp:
            out = Path(temp) / 'product'
            manifest = promotion.stage_product([plan], out)
            self.assertEqual(set(manifest), {plan['downloads'][0]['name'], 'SHA256SUMS.txt'})
            name = plan['downloads'][0]['name']
            source = plan['sources'][name]
            self.assertEqual(manifest[name], candidate['files'][source])
            self.assertIn(manifest[name]['sha256'] + '  ' + name, (out / 'SHA256SUMS.txt').read_text())
            self.assertEqual(promotion.stage_product([plan], out), manifest)
            (out / name).write_bytes(b'corrupt')
            with self.assertRaisesRegex(ValueError, 'Existing product payload differs'):
                promotion.stage_product([plan], out)

    def test_real_sealed_bridge_and_firmware_candidates_produce_one_scoped_product(self):
        fixture = fixtures.CandidateTests(methodName='runTest'); fixture.setUp(); self.addCleanup(fixture.doCleanups)
        bridge_dir, bridge = fixture.candidate(platform='windows', suffix='bridge')
        tab_dir, tab = fixture.candidate(platform='all', suffix='tab', component='tab5')
        entries = [{'directory':bridge_dir, 'acceptance':fixture.accepted(bridge_dir, bridge, suffix='bridge')},
                   {'directory':tab_dir, 'acceptance':fixture.accepted(tab_dir, tab, suffix='tab')}]
        result = promotion.promote_product(entries, 'owner/repo')
        self.assertFalse(result['execute'])
        self.assertEqual(set(result['components']), {'bridge-v' + bridge['version'], 'tab5-v' + tab['version']})
        plans = [promotion.component_plan(entry) for entry in entries]
        page = render_product('owner/repo', '0.6.21', plans)
        self.assertIn('桌面桥接 ' + bridge['version'], page['body'])
        self.assertIn('TAB5 ' + tab['version'], page['body'])
        self.assertNotIn('Mac 应用', page['body'])
        with tempfile.TemporaryDirectory() as temp:
            manifest = promotion.stage_product(plans, Path(temp) / 'product')
            self.assertEqual(len(manifest), 4)  # Windows, TAB5 first install, upgrade, checksums.

    def test_dry_run_is_offline_and_reports_only_selected_component(self):
        with patch.object(promotion, 'component_plan', return_value=self.tab5), patch.object(promotion.pipeline, 'require_release_scope', return_value={}), patch.object(promotion, 'reserve_product') as reserve:
            result = promotion.promote_product([{'directory':'fixture'}], 'owner/repo', publish=True)
            self.assertFalse(result['execute'])
            self.assertEqual(result['components'], ['tab5-v0.2.321-ui'])
            reserve.assert_not_called()

    def test_single_and_combined_publish_exactly_one_product_page(self):
        for components in ([self.tab5], [self.bridge], [self.bridge, self.tab5]):
            with self.subTest(components=len(components)):
                product = render_product('owner/repo', '0.6.21', components)
                release = {'tag_name':product['tag'], 'name':product['title'], 'body':product['body'],
                           'draft':True, 'prerelease':False, 'assets':[], 'html_url':'https://example.com/release'}
                def run_cli(args, **kwargs):
                    self.assertEqual(args[-1], '--latest=true')
                    release['draft'] = False
                    return SimpleNamespace(returncode=0, stdout=b'')
                manifest = {'fixture.zip': {'bytes': 10, 'sha256': 'a' * 64}}
                def upload(*args, **kwargs):
                    release['assets'] = [{'name':'fixture.zip', 'size':10, 'digest':'sha256:' + 'a' * 64}]
                entries = [{'directory':str(i), 'acceptance':'fixture'} for i in range(len(components))]
                with patch.object(promotion, 'component_plan', side_effect=components), patch.object(promotion.pipeline, 'require_release_scope', return_value={}), patch.object(promotion, 'stage_product', return_value=manifest), patch.object(promotion, 'reserve_product', return_value=(product, release)) as reserve, patch.object(promotion.pipeline, 'promote', side_effect=lambda **kwargs: {'tag': components[int(kwargs['directory'])]['candidate']['tag']} ) as promote, patch.object(promotion.pipeline, 'upload', side_effect=upload), patch.object(promotion.pipeline, 'closeout', return_value={}), patch.object(promotion.pipeline, 'remote_identity', side_effect=lambda *_: deepcopy(release)), patch.object(promotion.pipeline, 'run_cli', side_effect=run_cli) as cli:
                    result = promotion.promote_product(entries, 'owner/repo', execute=True, publish=True)
                    self.assertTrue(result['published'])
                    self.assertEqual(result['productTag'], 'v0.6.21')
                    self.assertEqual(promote.call_count, len(components))
                    reserve.assert_called_once(); cli.assert_called_once()

    def test_existing_matching_draft_is_reused(self):
        product = render_product('owner/repo', '0.6.21', [self.tab5])
        draft = {'tag_name':product['tag'], 'name':product['title'], 'body':product['body'],
                 'draft':True, 'prerelease':False, 'assets':[]}
        with patch.object(promotion, 'public_releases', return_value=[draft]), patch.object(promotion, 'orphan_matches', return_value=True), patch.object(promotion, 'ensure_product_tag') as ensure:
            actual, record = promotion.reserve_product('owner/repo', [self.tab5], {})
            self.assertEqual(actual, product); self.assertEqual(record, draft); ensure.assert_not_called()

    def test_failed_component_never_publishes_product_page(self):
        product = render_product('owner/repo', '0.6.21', [self.tab5])
        draft = {'tag_name':product['tag'], 'name':product['title'], 'body':product['body'],
                 'draft':True, 'prerelease':False, 'assets':[], 'html_url':'https://example.com'}
        with patch.object(promotion, 'component_plan', return_value=self.tab5), patch.object(promotion.pipeline, 'require_release_scope', return_value={}), patch.object(promotion, 'stage_product', return_value={}), patch.object(promotion, 'reserve_product', return_value=(product, draft)), patch.object(promotion.pipeline, 'promote', side_effect=ValueError('component failed')), patch.object(promotion.pipeline, 'upload') as upload, patch.object(promotion.pipeline, 'run_cli') as cli:
            with self.assertRaisesRegex(ValueError, 'component failed'):
                promotion.promote_product([{'directory':'fixture', 'acceptance':'fixture'}], 'owner/repo', execute=True, publish=True)
            self.assertTrue(draft['draft']); upload.assert_not_called(); cli.assert_not_called()

    def test_unknown_orphan_tag_blocks_instead_of_skipping_version(self):
        with patch.object(promotion, 'public_releases', return_value=[{'tag_name':'v0.6.20', 'body':''}]), patch.object(promotion, 'product_tags', return_value=['v0.6.20','v0.6.21']), patch.object(promotion, 'orphan_matches', return_value=False), patch.object(promotion, 'ensure_product_tag') as ensure:
            with self.assertRaisesRegex(ValueError, 'no release'):
                promotion.reserve_product('owner/repo', [self.tab5], {})
            ensure.assert_not_called()

    def test_unrelated_draft_cannot_claim_next_product_tag(self):
        releases = [{'tag_name':'v0.6.20', 'body':''},
                    {'tag_name':'v0.6.21', 'body':'unrelated draft', 'draft':True}]
        with patch.object(promotion, 'public_releases', return_value=releases), patch.object(promotion, 'product_tags', return_value=['v0.6.20']), patch.object(promotion, 'ensure_product_tag') as ensure:
            with self.assertRaisesRegex(ValueError, 'unrelated release'):
                promotion.reserve_product('owner/repo', [self.tab5], {})
            ensure.assert_not_called()

    def test_outstanding_product_draft_prevents_out_of_order_release(self):
        other = {'tag_name':'v0.6.21', 'body':'<!-- aibot-product-release:' + 'f'*64 + ' -->', 'draft':True}
        with patch.object(promotion, 'public_releases', return_value=[other]), patch.object(promotion, 'product_tags', return_value=['v0.6.20','v0.6.21']), patch.object(promotion, 'ensure_product_tag') as ensure:
            with self.assertRaisesRegex(ValueError, 'draft is already reserved'):
                promotion.reserve_product('owner/repo', [self.tab5], {})
            ensure.assert_not_called()

    def test_older_reserved_product_cannot_supersede_newer_public_release(self):
        product = render_product('owner/repo', '0.6.21', [self.tab5])
        draft = {'tag_name':product['tag'], 'name':product['title'], 'body':product['body'], 'draft':True, 'prerelease':False, 'assets':[]}
        newer = {'tag_name':'v0.6.22', 'body':'', 'draft':False, 'prerelease':False}
        with patch.object(promotion, 'public_releases', return_value=[draft, newer]):
            with self.assertRaisesRegex(ValueError, 'newer product release'):
                promotion.reserve_product('owner/repo', [self.tab5], {})

    def test_orphan_from_same_candidate_reuses_reserved_number(self):
        product = render_product('owner/repo', '0.6.21', [self.tab5])
        draft = {'tag_name':product['tag'], 'name':product['title'], 'body':product['body'],
                 'draft':True, 'prerelease':False, 'assets':[]}
        with patch.object(promotion, 'public_releases', return_value=[{'tag_name':'v0.6.20', 'body':''}]), patch.object(promotion, 'product_tags', return_value=['v0.6.20','v0.6.21']), patch.object(promotion, 'orphan_matches', return_value=True), patch.object(promotion, 'ensure_product_tag') as ensure, patch.object(promotion.pipeline, 'run_cli') as cli, patch.object(promotion.pipeline, 'remote_identity', return_value=draft):
            actual, _ = promotion.reserve_product('owner/repo', [self.tab5], {})
            self.assertEqual(actual['tag'], 'v0.6.21')
            ensure.assert_not_called(); cli.assert_called_once()

    def test_interrupted_or_retried_promotion_reuses_one_product_release(self):
        product = render_product('owner/repo', '0.6.21', [self.tab5])
        manifest = {'fixture.zip': {'bytes': 10, 'sha256': 'a' * 64}}
        state = {'release': None, 'created': 0, 'uploaded': 0, 'published': 0, 'tags': ['v0.6.20']}
        def releases(_):
            return [{'tag_name':'v0.6.20', 'body':''}] + ([deepcopy(state['release'])] if state['release'] else [])
        def ensure(_, tag, identity): state['tags'].append(tag)
        def run_cli(args, **_):
            if args[:2] == ['release', 'create']:
                state['created'] += 1
                self.assertEqual(Path(args[args.index('--notes-file') + 1]).read_text(encoding='utf-8'), product['body'])
                state['release'] = {'tag_name':product['tag'], 'name':product['title'], 'body':product['body'], 'draft':True, 'prerelease':False, 'assets':[], 'html_url':'https://example.com/release'}
            elif args[:2] == ['release', 'edit']:
                state['published'] += 1; state['release']['draft'] = False
            else: self.fail('Unexpected GitHub mutation')
            return SimpleNamespace(returncode=0, stdout=b'')
        def upload(*args, **_):
            state['uploaded'] += 1
            state['release']['assets'] = [{'name':'fixture.zip', 'size':10, 'digest':'sha256:' + 'a' * 64}]
        entry = {'directory':'fixture', 'acceptance':'fixture'}
        with patch.object(promotion, 'component_plan', return_value=self.tab5), patch.object(promotion.pipeline, 'require_release_scope', return_value={}), patch.object(promotion, 'stage_product', return_value=manifest), patch.object(promotion, 'public_releases', side_effect=releases), patch.object(promotion, 'product_tags', side_effect=lambda: state['tags']), patch.object(promotion, 'ensure_product_tag', side_effect=ensure), patch.object(promotion, 'orphan_matches', return_value=True), patch.object(promotion.pipeline, 'run_cli', side_effect=run_cli), patch.object(promotion.pipeline, 'remote_identity', side_effect=lambda *_: deepcopy(state['release'])), patch.object(promotion.pipeline, 'promote', return_value={'tag':'tab5-v0.2.321-ui'}), patch.object(promotion.pipeline, 'upload', side_effect=upload), patch.object(promotion.pipeline, 'closeout', return_value={}):
            first = promotion.promote_product([entry], 'owner/repo', execute=True, publish=True)
            second = promotion.promote_product([entry], 'owner/repo', execute=True, publish=True)
            self.assertEqual(first['productTag'], 'v0.6.21')
            self.assertEqual(second['productTag'], first['productTag'])
            self.assertEqual((state['created'], state['uploaded'], state['published']), (1, 1, 1))


if __name__ == '__main__': unittest.main()
