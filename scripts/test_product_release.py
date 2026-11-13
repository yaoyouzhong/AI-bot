"""Product release identity, independent numbering, and scoped notes."""
import unittest

from product_release import next_product_version, plan_identity, render_product


def component(kind, version, tag, candidate_id, names):
    title = {'bridge': '桌面桥接', 'tab5': 'TAB5', 'esp8266': 'ESP8266'}[kind]
    return {'candidate': {'component': kind, 'version': version, 'tag': tag, 'candidateId': candidate_id * 64},
            'downloads': [{'name': name, 'label': name} for name in names],
            'notes': f'## 下载\n\n旧入口\n\n## {title} {version}\n\n- 中文更新\n\n## English\n\n- English update\n'}


class ProductReleaseTests(unittest.TestCase):
    def test_product_sequence_ignores_independent_component_tags(self):
        self.assertEqual(next_product_version(['v0.6.20', 'bridge-v0.6.27', 'tab5-v0.2.321-ui']), '0.6.21')
        self.assertEqual(next_product_version(['v0.6.20', 'v0.6.21', 'esp8266-v0.9.0']), '0.6.22')
        with self.assertRaises(ValueError): next_product_version(['tab5-v0.2.320-ui'])

    def test_single_firmware_product_does_not_invent_bridge(self):
        tab = component('tab5', '0.2.321-ui', 'tab5-v0.2.321-ui', 'a', ['AI-bot-tab5-0.2.321-ui-upgrade.zip'])
        product = render_product('owner/repo', '0.6.21', [tab])
        self.assertEqual(product['title'], 'AI-bot v0.6.21')
        self.assertEqual(product['tag'], 'v0.6.21')
        self.assertIn('TAB5 0.2.321-ui', product['body'])
        self.assertIn('/download/v0.6.21/AI-bot-tab5-0.2.321-ui-upgrade.zip', product['body'])
        self.assertIn('/tag/tab5-v0.2.321-ui', product['body'])
        self.assertNotIn('桌面桥接', product['body'])
        self.assertIn('English update', product['body'])

    def test_bridge_and_combined_product_follow_actual_scope(self):
        bridge = component('bridge', '0.6.20', 'bridge-v0.6.20', 'b', ['AI-bot-bridge-v0.6.20-macos-arm64.zip'])
        tab = component('tab5', '0.2.321-ui', 'tab5-v0.2.321-ui', 'a', ['AI-bot-tab5-0.2.321-ui-upgrade.zip'])
        single = render_product('owner/repo', '0.6.21', [bridge])
        self.assertNotIn('TAB5', single['body'])
        self.assertNotIn('windows-x64', single['body'])
        combined = render_product('owner/repo', '0.6.22', [bridge, tab])
        self.assertIn('桌面桥接 0.6.20', combined['body'])
        self.assertIn('TAB5 0.2.321-ui', combined['body'])
        self.assertEqual(plan_identity([bridge, tab]), plan_identity([tab, bridge]))
        with self.assertRaises(ValueError): render_product('owner/repo', '0.6.22', [bridge, bridge])
        contaminated = dict(bridge)
        contaminated['notes'] = bridge['notes'].replace('## English', '## TAB5 0.2.321-ui\n\n- 未选组件\n\n## English')
        with self.assertRaisesRegex(ValueError, 'standard release template'):
            render_product('owner/repo', '0.6.21', [contaminated])


if __name__ == '__main__': unittest.main()
