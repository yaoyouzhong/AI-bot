import hashlib
import json
from pathlib import Path
from tempfile import TemporaryDirectory
from types import SimpleNamespace
import unittest
from unittest.mock import patch

import upload_release_resumable as uploader


class ResumableUploadTests(unittest.TestCase):
    def test_split_reassembly_and_interrupted_local_split(self):
        with TemporaryDirectory() as directory:
            root = Path(directory)
            source = root / 'Gallery.zip'
            data = bytes(range(256)) * 100
            source.write_bytes(data)
            output = root / 'parts'
            first = uploader.split([source], output, 4096)
            complete = output / first['archives'][0]['parts'][0]['name']
            timestamp = complete.stat().st_mtime_ns
            second = output / first['archives'][0]['parts'][1]['name']
            second.write_bytes(b'interrupted')
            resumed = uploader.split([source], output, 4096)
            self.assertEqual(timestamp, complete.stat().st_mtime_ns)
            archive = resumed['archives'][0]
            self.assertEqual(data, b''.join((output / p['name']).read_bytes() for p in archive['parts']))
            self.assertEqual(hashlib.sha256(data).hexdigest(), archive['sha256'])

    def test_lost_response_and_restart_skip_verified_server_parts(self):
        with TemporaryDirectory() as directory:
            root = Path(directory)
            manifest = {}
            for name in ['one.part001', 'two.part002']:
                path = root / name
                path.write_bytes(name.encode())
                manifest[name] = {'bytes': path.stat().st_size, 'sha256': uploader.digest(path)}
            assets = []
            calls = []

            def fake(args, **kwargs):
                if args[:2] == ['release', 'view']:
                    result = {'databaseId': 123, 'tagName': 'test', 'isDraft': True}
                elif args[0] == 'api':
                    result = {'tag_name': 'test', 'assets': assets.copy()}
                else:
                    name = Path(args[3]).name
                    calls.append(name)
                    assets.append({'id': len(assets) + 1, 'name': name, 'state': 'uploaded',
                                   'size': manifest[name]['bytes'], 'digest': 'sha256:' + manifest[name]['sha256']})
                    if len(calls) == 1:
                        raise ConnectionError('Response lost after the server stored the part')
                    result = {}
                return SimpleNamespace(stdout=json.dumps(result).encode())

            with patch.object(uploader, 'run_cli', side_effect=fake), patch.object(uploader.time, 'sleep'):
                uploader.upload('owner/repo', 'test', root, manifest, root / 'state.json', workers=1)
                self.assertEqual(2, len(calls))
                # Local state may be lost; server hashes still prevent re-upload.
                (root / 'state.json').unlink()
                uploader.upload('owner/repo', 'test', root, manifest, root / 'state.json', workers=1)
                self.assertEqual(2, len(calls))
            self.assertEqual(2, len(json.loads((root / 'state.json').read_text())['verified']))

    def test_local_hash_failure_prevents_network_mutation(self):
        with TemporaryDirectory() as directory:
            root = Path(directory)
            (root / 'bad.zip').write_bytes(b'changed')
            with patch.object(uploader, 'run_cli') as cli, self.assertRaises(ValueError):
                uploader.upload('o/r', 't', root, {'bad.zip': {'bytes': 7, 'sha256': '0' * 64}}, root / 'state.json')
            cli.assert_not_called()


if __name__ == '__main__':
    unittest.main()
